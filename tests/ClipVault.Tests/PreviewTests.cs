using ClipVault.Core;
using Xunit;

namespace ClipVault.Tests;

public class PreviewTests
{
    [Fact]
    public void Row_collapses_whitespace_and_trims()
    {
        Assert.Equal("a b c", Preview.Row("  a\r\n\tb   c \n"));
    }

    [Fact]
    public void Row_truncates_with_ellipsis_at_limit()
    {
        var text = new string('x', Preview.RowLength + 10);
        var row = Preview.Row(text);
        Assert.Equal(Preview.RowLength, row.Length);
        Assert.EndsWith("…", row);
    }

    [Fact]
    public void Row_keeps_exact_length_text_untouched()
    {
        var text = new string('x', Preview.RowLength);
        Assert.Equal(text, Preview.Row(text));
    }

    [Fact]
    public void Truncate_does_not_split_surrogate_pairs()
    {
        var emoji = string.Concat(Enumerable.Repeat("😀", 70));
        var row = Preview.Row(emoji);
        Assert.Equal(string.Concat(Enumerable.Repeat("😀", Preview.RowLength - 1)) + "…", row);
    }

    [Fact]
    public void RowIsTruncated_only_when_collapsed_text_exceeds_row()
    {
        Assert.False(Preview.RowIsTruncated(new string('x', Preview.RowLength)));
        Assert.True(Preview.RowIsTruncated(new string('x', Preview.RowLength + 1)));
        // Whitespace is collapsed before measuring, so padding does not count.
        Assert.False(Preview.RowIsTruncated("short   text\n\n" + new string(' ', 100)));
        Assert.False(Preview.RowIsTruncated(string.Concat(Enumerable.Repeat("😀", Preview.RowLength))));
    }

    [Fact]
    public void Tooltip_keeps_newlines()
    {
        Assert.Equal("a\nb", Preview.Tooltip("a\nb"));
    }

    [Fact]
    public void Non_latin_text_survives()
    {
        const string text = "Tere, мир, 世界, مرحبا";
        Assert.Equal(text, Preview.Row(text));
    }
}
