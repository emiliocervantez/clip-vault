namespace ClipVault.Core;

public static class Limits
{
    /// <summary>Largest text clip stored, measured in UTF-16 bytes.</summary>
    public const int MaxTextBytes = 1 * 1024 * 1024;

    /// <summary>Largest image clip stored, measured in decoded pixel bytes.</summary>
    public const int MaxImageBytes = 16 * 1024 * 1024;

    public static bool TextFits(string text) => (long)text.Length * 2 <= MaxTextBytes;

    public static bool ImageFits(long decodedBytes) => decodedBytes <= MaxImageBytes;
}
