using System.Globalization;
using System.Text.RegularExpressions;

namespace ClipVault.Core;

/// <summary>Formats clip text for display in the popup.</summary>
public static partial class Preview
{
    public const int RowLength = 60;
    public const int TooltipLength = 500;

    /// <summary>Single line: whitespace runs collapsed, trimmed, truncated with an ellipsis.</summary>
    public static string Row(string text) => Truncate(Whitespace().Replace(text, " ").Trim(), RowLength);

    /// <summary>Original text, truncated with an ellipsis.</summary>
    public static string Tooltip(string text) => Truncate(text, TooltipLength);

    /// <summary>Truncates on text-element boundaries so surrogate pairs and combining marks stay intact.</summary>
    public static string Truncate(string text, int maxElements)
    {
        var info = new StringInfo(text);
        if (info.LengthInTextElements <= maxElements) return text;
        return info.SubstringByTextElements(0, maxElements - 1) + "…";
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
