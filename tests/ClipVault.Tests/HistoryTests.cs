using ClipVault.Core;
using Xunit;

namespace ClipVault.Tests;

public class HistoryTests
{
    private static History Filled(int max, params string[] texts)
    {
        var h = new History(max);
        foreach (var t in texts) h.Add(Clip.ForText(t));
        return h;
    }

    private static string[] Texts(History h) => h.Items.Select(c => c.Text!).ToArray();

    [Fact]
    public void Add_puts_newest_first()
    {
        var h = Filled(10, "a", "b", "c");
        Assert.Equal(new[] { "c", "b", "a" }, Texts(h));
    }

    [Fact]
    public void Add_evicts_oldest_beyond_cap_and_reports_it()
    {
        var h = new History(2);
        var removed = new List<string>();
        h.Removed += c => removed.Add(c.Text!);
        h.Add(Clip.ForText("a"));
        h.Add(Clip.ForText("b"));
        h.Add(Clip.ForText("c"));
        Assert.Equal(new[] { "c", "b" }, Texts(h));
        Assert.Equal(new[] { "a" }, removed);
    }

    [Fact]
    public void Add_duplicate_replaces_and_moves_to_top()
    {
        var h = Filled(10, "a", "b", "c");
        h.Add(Clip.ForText("a"));
        Assert.Equal(new[] { "a", "c", "b" }, Texts(h));
        Assert.Equal(3, h.Items.Count);
    }

    [Fact]
    public void Touch_moves_existing_to_top_without_adding()
    {
        var h = Filled(10, "a", "b", "c");
        var first = h.Items[2];
        Assert.True(h.Touch(ContentHash.OfText("a")));
        Assert.Same(first, h.Items[0]);
        Assert.False(h.Touch(ContentHash.OfText("zzz")));
        Assert.Equal(3, h.Items.Count);
    }

    [Fact]
    public void Choose_sets_last_chosen_and_keeps_position_when_not_moving()
    {
        var h = Filled(10, "a", "b", "c");
        var a = h.Items[2];
        h.Choose(a.Id, moveToTop: false);
        Assert.Equal(a.Id, h.LastChosenId);
        Assert.Equal(new[] { "c", "b", "a" }, Texts(h));
    }

    [Fact]
    public void Choose_moves_to_top_when_asked()
    {
        var h = Filled(10, "a", "b", "c");
        var a = h.Items[2];
        h.Choose(a.Id, moveToTop: true);
        Assert.Equal(new[] { "a", "c", "b" }, Texts(h));
        Assert.Equal(a.Id, h.LastChosenId);
    }

    [Fact]
    public void Remove_clears_last_chosen_if_it_was_removed()
    {
        var h = Filled(10, "a", "b");
        var b = h.Items[0];
        h.Choose(b.Id, false);
        Assert.True(h.Remove(b.Id));
        Assert.Null(h.LastChosenId);
        Assert.Equal(new[] { "a" }, Texts(h));
        Assert.False(h.Remove(b.Id));
    }

    [Fact]
    public void Clear_reports_every_clip()
    {
        var h = Filled(10, "a", "b", "c");
        var removed = 0;
        h.Removed += _ => removed++;
        h.Clear();
        Assert.Empty(h.Items);
        Assert.Equal(3, removed);
        Assert.Null(h.LastChosenId);
    }

    [Fact]
    public void SetMaxItems_shrinks_and_clamps()
    {
        var h = Filled(10, "a", "b", "c", "d");
        h.SetMaxItems(2);
        Assert.Equal(new[] { "d", "c" }, Texts(h));
        h.SetMaxItems(0);
        Assert.Equal(Settings.MinItems, h.MaxItems);
        h.SetMaxItems(100000);
        Assert.Equal(Settings.MaxItemsLimit, h.MaxItems);
    }

    [Fact]
    public void Changed_fires_on_mutations()
    {
        var h = new History(5);
        var n = 0;
        h.Changed += () => n++;
        h.Add(Clip.ForText("a"));
        h.Touch(ContentHash.OfText("a"));
        h.Choose(h.Items[0].Id, false);
        h.Remove(h.Items[0].Id);
        Assert.Equal(4, n);
    }
}
