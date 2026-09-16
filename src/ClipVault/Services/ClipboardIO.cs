using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static ClipVault.Native.NativeMethods;

namespace ClipVault.Services;

/// <summary>What the clipboard held at one moment: text, an image, or nothing we handle.</summary>
internal sealed record ClipboardSnapshot(string? Text, BitmapSource? Image)
{
    public static readonly ClipboardSnapshot Empty = new(null, null);
}

/// <summary>
/// Raw Win32 clipboard access. Deliberately not WPF's Clipboard class: that goes through OLE, which
/// publishes delayed-render placeholders and lets other processes hold references to our data object.
/// Chromium-based apps (Slack) then call back into this process during our next write and both sides
/// stall for seconds. Here every format is written fully rendered inside one open/close, so no other
/// process ever has to talk to ClipVault.
/// </summary>
internal static class ClipboardIO
{
    private const uint CF_DIB = 8;
    private const uint CF_UNICODETEXT = 13;
    private const int BmpFileHeaderSize = 14;
    private const int Attempts = 6;
    private const int RetryDelayMs = 40;

    private static readonly uint ExcludeFormat = RegisterClipboardFormat("ExcludeClipboardContentFromMonitorProcessing");
    private static readonly uint PngFormat = RegisterClipboardFormat("PNG");

    /// <summary>Window that owns clipboard content written by ClipVault. Set once at startup.</summary>
    public static IntPtr Owner { get; set; }

    /// <summary>Returns null when the content is flagged as private by its source (password managers).</summary>
    public static ClipboardSnapshot? Read()
    {
        if (!Open()) return ClipboardSnapshot.Empty;
        try
        {
            if (IsClipboardFormatAvailable(ExcludeFormat)) return null;
            if (IsClipboardFormatAvailable(CF_UNICODETEXT))
                return new ClipboardSnapshot(ReadText(), null);
            if (IsClipboardFormatAvailable(PngFormat) && ReadBytes(PngFormat) is { } png)
                return new ClipboardSnapshot(null, Decode(new PngBitmapDecoder(new MemoryStream(png), BitmapCreateOptions.None, BitmapCacheOption.OnLoad)));
            if (IsClipboardFormatAvailable(CF_DIB) && ReadBytes(CF_DIB) is { } dib)
                return new ClipboardSnapshot(null, DecodeDib(dib));
            return ClipboardSnapshot.Empty;
        }
        finally
        {
            CloseClipboard();
        }
    }

    public static void SetText(string text)
    {
        if (!Open()) return;
        try
        {
            EmptyClipboard();
            var bytes = new byte[(text.Length + 1) * 2];
            System.Text.Encoding.Unicode.GetBytes(text, 0, text.Length, bytes, 0);
            Put(CF_UNICODETEXT, bytes);
        }
        finally
        {
            CloseClipboard();
        }
    }

    /// <summary>Writes CF_DIB (universal) plus PNG (keeps alpha for apps that prefer it).</summary>
    public static void SetImage(BitmapSource image)
    {
        var png = ImageCodec.EncodePng(image);
        var bmp = EncodeBmp(image);
        if (!Open()) return;
        try
        {
            EmptyClipboard();
            Put(CF_DIB, bmp.AsSpan(BmpFileHeaderSize).ToArray());
            Put(PngFormat, png);
        }
        finally
        {
            CloseClipboard();
        }
    }

    public static void Restore(ClipboardSnapshot snapshot)
    {
        if (snapshot.Text is not null) SetText(snapshot.Text);
        else if (snapshot.Image is not null) SetImage(snapshot.Image);
    }

    private static bool Open()
    {
        for (var i = 0; i < Attempts; i++)
        {
            if (OpenClipboard(Owner)) return true;
            Thread.Sleep(RetryDelayMs);   // another process has it open for a moment
        }
        return false;
    }

    private static string? ReadText()
    {
        var handle = GetClipboardData(CF_UNICODETEXT);
        if (handle == IntPtr.Zero) return null;
        var ptr = GlobalLock(handle);
        if (ptr == IntPtr.Zero) return null;
        try
        {
            return Marshal.PtrToStringUni(ptr);
        }
        finally
        {
            GlobalUnlock(handle);
        }
    }

    private static byte[]? ReadBytes(uint format)
    {
        var handle = GetClipboardData(format);
        if (handle == IntPtr.Zero) return null;
        var ptr = GlobalLock(handle);
        if (ptr == IntPtr.Zero) return null;
        try
        {
            var size = (int)GlobalSize(handle);
            var bytes = new byte[size];
            Marshal.Copy(ptr, bytes, 0, size);
            return bytes;
        }
        finally
        {
            GlobalUnlock(handle);
        }
    }

    /// <summary>Hands a movable global block to the clipboard; the system owns it after a successful call.</summary>
    private static void Put(uint format, byte[] bytes)
    {
        var handle = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)bytes.Length);
        if (handle == IntPtr.Zero) return;
        var ptr = GlobalLock(handle);
        Marshal.Copy(bytes, 0, ptr, bytes.Length);
        GlobalUnlock(handle);
        if (SetClipboardData(format, handle) == IntPtr.Zero) GlobalFree(handle);
    }

    private static BitmapSource? Decode(BitmapDecoder decoder)
    {
        try
        {
            var frame = decoder.Frames[0];
            frame.Freeze();
            return frame;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>A CF_DIB block is a BMP file without its 14-byte file header; rebuild the header and decode.</summary>
    private static BitmapSource? DecodeDib(byte[] dib)
    {
        if (dib.Length < 40) return null;
        var headerSize = BitConverter.ToInt32(dib, 0);
        var bitCount = BitConverter.ToUInt16(dib, 14);
        var compression = BitConverter.ToUInt32(dib, 16);
        var colorsUsed = BitConverter.ToInt32(dib, 32);
        var colorTable = bitCount <= 8 ? (colorsUsed > 0 ? colorsUsed : 1 << bitCount) * 4 : 0;
        var masks = compression == 3 /* BI_BITFIELDS */ && headerSize == 40 ? 12 : 0;
        var pixelOffset = BmpFileHeaderSize + headerSize + masks + colorTable;

        var bmp = new byte[BmpFileHeaderSize + dib.Length];
        bmp[0] = (byte)'B';
        bmp[1] = (byte)'M';
        BitConverter.GetBytes(bmp.Length).CopyTo(bmp, 2);
        BitConverter.GetBytes(pixelOffset).CopyTo(bmp, 10);
        dib.CopyTo(bmp, BmpFileHeaderSize);
        return Decode(new BmpBitmapDecoder(new MemoryStream(bmp), BitmapCreateOptions.None, BitmapCacheOption.OnLoad));
    }

    /// <summary>Encodes as a plain 32-bit BI_RGB bitmap so every consumer of CF_DIB understands it.</summary>
    private static byte[] EncodeBmp(BitmapSource image)
    {
        var opaque = new FormatConvertedBitmap(image, PixelFormats.Bgr32, null, 0);
        var encoder = new BmpBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(opaque));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        return ms.ToArray();
    }
}
