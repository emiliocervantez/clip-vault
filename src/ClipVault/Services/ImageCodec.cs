using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClipVault.Core;

namespace ClipVault.Services;

internal static class ImageCodec
{
    public static byte[] EncodePng(BitmapSource image)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        return ms.ToArray();
    }

    /// <summary>Loads a PNG fully into memory so the file stays unlocked.</summary>
    public static BitmapSource Load(string path, int? decodeHeight = null)
    {
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.UriSource = new Uri(path);
        if (decodeHeight is int h) bmp.DecodePixelHeight = h;
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }

    /// <summary>Hash of the BGRA pixel bytes, or null when the image exceeds the size limit.</summary>
    public static string? PixelHash(BitmapSource image)
    {
        var bgra = image.Format == PixelFormats.Bgra32 ? image : new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
        var stride = bgra.PixelWidth * 4;
        var bytes = (long)stride * bgra.PixelHeight;
        if (!Limits.ImageFits(bytes)) return null;
        var buffer = new byte[bytes];
        bgra.CopyPixels(buffer, stride, 0);
        return ContentHash.OfBytes(buffer);
    }
}
