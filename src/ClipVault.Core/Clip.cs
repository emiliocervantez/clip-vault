namespace ClipVault.Core;

public enum ClipKind { Text, Image }

/// <summary>One captured clipboard entry. Immutable.</summary>
public sealed class Clip
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public ClipKind Kind { get; init; }
    /// <summary>Full text for <see cref="ClipKind.Text"/>; null for images.</summary>
    public string? Text { get; init; }
    /// <summary>PNG file name (relative to the images directory) for <see cref="ClipKind.Image"/>; null for text.</summary>
    public string? ImageFile { get; init; }
    /// <summary>Content hash used for deduplication.</summary>
    public string Hash { get; init; } = "";
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    public static Clip ForText(string text) => new()
    {
        Kind = ClipKind.Text,
        Text = text,
        Hash = ContentHash.OfText(text),
    };

    public static Clip ForImage(Guid id, string imageFile, string hash) => new()
    {
        Id = id,
        Kind = ClipKind.Image,
        ImageFile = imageFile,
        Hash = hash,
    };
}
