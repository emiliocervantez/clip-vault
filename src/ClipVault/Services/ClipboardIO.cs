using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ClipVault.Services;

/// <summary>What the clipboard held at one moment: text, an image, or nothing we handle.</summary>
internal sealed record ClipboardSnapshot(string? Text, BitmapSource? Image)
{
    public static readonly ClipboardSnapshot Empty = new(null, null);
}

/// <summary>Thin wrapper over the WPF clipboard with retry, because other processes hold it briefly after writing.</summary>
internal static class ClipboardIO
{
    private const string ExcludeFormat = "ExcludeClipboardContentFromMonitorProcessing";
    private const int Attempts = 6;
    private const int RetryDelayMs = 40;

    /// <summary>Returns null when the content is flagged as private by its source (password managers).</summary>
    public static ClipboardSnapshot? Read()
    {
        return Retry(() =>
        {
            var data = Clipboard.GetDataObject();
            if (data is null) return ClipboardSnapshot.Empty;
            if (data.GetDataPresent(ExcludeFormat)) return null;
            if (data.GetDataPresent(DataFormats.UnicodeText))
                return new ClipboardSnapshot(data.GetData(DataFormats.UnicodeText) as string, null);
            if (Clipboard.ContainsImage())
                return new ClipboardSnapshot(null, Clipboard.GetImage());
            return ClipboardSnapshot.Empty;
        });
    }

    public static void SetText(string text) => Retry(() => { Clipboard.SetDataObject(text, copy: true); return true; });

    public static void SetImage(BitmapSource image) => Retry(() => { Clipboard.SetImage(image); return true; });

    public static void Restore(ClipboardSnapshot snapshot)
    {
        if (snapshot.Text is not null) SetText(snapshot.Text);
        else if (snapshot.Image is not null) SetImage(snapshot.Image);
    }

    private static T? Retry<T>(Func<T?> action)
    {
        for (var i = 0; ; i++)
        {
            try
            {
                return action();
            }
            catch (COMException) when (i < Attempts - 1)
            {
                Thread.Sleep(RetryDelayMs);
            }
            catch (COMException)
            {
                return default;
            }
        }
    }
}
