using ClipVault.Core;
using Xunit;

namespace ClipVault.Tests;

public sealed class VaultStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ClipVaultTests_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void History_round_trip_preserves_order_and_last_chosen()
    {
        var storage = new VaultStorage(_root);
        var h = new History(10);
        h.Add(Clip.ForText("first"));
        h.Add(Clip.ForText("second 世界"));
        h.Choose(h.Items[1].Id, false);
        storage.SaveHistory(h);

        var data = storage.LoadHistory();
        Assert.Equal(new[] { "second 世界", "first" }, data.Items.Select(c => c.Text));
        Assert.Equal(h.LastChosenId, data.LastChosenId);
        Assert.Equal(h.Items[0].Hash, data.Items[0].Hash);
    }

    [Fact]
    public void Image_clips_without_file_are_dropped_on_load()
    {
        var storage = new VaultStorage(_root);
        var id = Guid.NewGuid();
        var file = storage.SaveImage(id, new byte[] { 1, 2, 3 });
        var kept = Clip.ForImage(id, file, "h1");
        var missing = Clip.ForImage(Guid.NewGuid(), "nope.png", "h2");
        var h = new History(10, new[] { kept, missing });
        storage.SaveHistory(h);

        var data = storage.LoadHistory();
        Assert.Single(data.Items);
        Assert.Equal(id, data.Items[0].Id);
    }

    [Fact]
    public void DeleteImage_and_orphans()
    {
        var storage = new VaultStorage(_root);
        var a = Clip.ForImage(Guid.NewGuid(), "", "a");
        a = Clip.ForImage(a.Id, storage.SaveImage(a.Id, new byte[] { 1 }), "a");
        var orphanId = Guid.NewGuid();
        storage.SaveImage(orphanId, new byte[] { 2 });

        storage.DeleteOrphanImages(new[] { a });
        Assert.True(File.Exists(storage.ImagePath(a)));
        Assert.Single(Directory.GetFiles(storage.ImagesDir));

        storage.DeleteImage(a);
        Assert.Empty(Directory.GetFiles(storage.ImagesDir));
    }

    [Fact]
    public void Missing_or_corrupt_files_yield_defaults()
    {
        var storage = new VaultStorage(_root);
        Assert.Equal(Settings.DefaultMaxItems, storage.LoadSettings().MaxItems);
        File.WriteAllText(Path.Combine(_root, "settings.json"), "{not json");
        Assert.Equal(Settings.DefaultMaxItems, storage.LoadSettings().MaxItems);
        Assert.Empty(storage.LoadHistory().Items);
    }

    [Fact]
    public void Settings_round_trip()
    {
        var storage = new VaultStorage(_root);
        var s = new Settings { MaxItems = 123, MoveChosenToTop = true };
        storage.SaveSettings(s);
        var back = storage.LoadSettings();
        Assert.Equal(123, back.MaxItems);
        Assert.True(back.MoveChosenToTop);
    }
}
