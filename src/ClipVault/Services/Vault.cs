using System.Windows.Media.Imaging;
using ClipVault.Core;
using ClipVault.Native;

namespace ClipVault.Services;

/// <summary>Owns settings, history and storage. Captures from and writes to the clipboard. UI-thread only.</summary>
internal sealed class Vault
{
    private uint _ownSequence;

    public Vault(VaultStorage storage)
    {
        Storage = storage;
        Settings = storage.LoadSettings();
        var data = storage.LoadHistory();
        History = new History(Settings.MaxItems, data.Items, data.LastChosenId);
        storage.DeleteOrphanImages(History.Items);
        History.Removed += storage.DeleteImage;
        History.Changed += () => storage.SaveHistory(History);
    }

    public VaultStorage Storage { get; }
    public Settings Settings { get; private set; }
    public History History { get; }

    /// <summary>Called on every clipboard change. Ignores our own writes, private content and oversized data.</summary>
    public void CaptureFromClipboard()
    {
        var sequence = NativeMethods.GetClipboardSequenceNumber();
        Trace.Log($"clipboard update: seq {sequence}, own {_ownSequence}{(sequence == _ownSequence ? " (skipped)" : "")}");
        if (sequence == _ownSequence) return;

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var snapshot = ClipboardIO.Read();
        Trace.Log($"clipboard read: {sw.ElapsedMilliseconds} ms, {(snapshot is null ? "private" : snapshot.Text is not null ? "text" : snapshot.Image is not null ? "image" : "unsupported")}");
        if (snapshot is null) return;

        if (snapshot.Text is { } text)
        {
            if (text.Length == 0 || !Limits.TextFits(text)) return;
            if (History.Touch(ContentHash.OfText(text))) return;
            History.Add(Clip.ForText(text));
        }
        else if (snapshot.Image is { } image)
        {
            var hash = ImageCodec.PixelHash(image);
            if (hash is null) return;
            if (History.Touch(hash)) return;
            var id = Guid.NewGuid();
            var file = Storage.SaveImage(id, ImageCodec.EncodePng(image));
            History.Add(Clip.ForImage(id, file, hash));
        }
    }

    /// <summary>Marks the clip as chosen and puts it on the clipboard without re-capturing it.</summary>
    public void Choose(Clip clip)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        History.Choose(clip.Id, Settings.MoveChosenToTop);
        Trace.Log($"choose: history updated in {sw.ElapsedMilliseconds} ms");
        if (clip.Kind == ClipKind.Text) SetOwnText(clip.Text!);
        else SetOwnImage(ImageCodec.Load(Storage.ImagePath(clip)));
        Trace.Log($"choose: clipboard written, total {sw.ElapsedMilliseconds} ms");
    }

    public void SetOwnText(string text)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        ClipboardIO.SetText(text);
        MarkOwnWrite();
        Trace.Log($"clipboard write: {text.Length} chars in {sw.ElapsedMilliseconds} ms, seq now {_ownSequence}");
    }

    public void SetOwnImage(BitmapSource image)
    {
        ClipboardIO.SetImage(image);
        MarkOwnWrite();
    }

    public void RestoreOwn(ClipboardSnapshot snapshot)
    {
        ClipboardIO.Restore(snapshot);
        MarkOwnWrite();
    }

    private void MarkOwnWrite() => _ownSequence = NativeMethods.GetClipboardSequenceNumber();

    public void UpdateSettings(Settings settings)
    {
        Settings = settings;
        History.SetMaxItems(settings.MaxItems);
        Storage.SaveSettings(settings);
        StartupRegistry.Set(settings.StartWithWindows);
    }
}
