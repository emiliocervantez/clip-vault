namespace ClipVault.Core;

public sealed class Template
{
    public string Name { get; set; } = "";
    public string Text { get; set; } = "";
    /// <summary>Optional. A template without a hotkey is reachable only from the popup submenu.</summary>
    public Hotkey? Hotkey { get; set; }

    public Template Clone() => new()
    {
        Name = Name,
        Text = Text,
        Hotkey = Hotkey is null ? null : new Hotkey(Hotkey.Modifiers, Hotkey.VirtualKey),
    };
}

public sealed class Settings
{
    public const int MinItems = 1;
    public const int MaxItemsLimit = 500;
    public const int DefaultMaxItems = 50;
    private const int VkV = 0x56;

    public int MaxItems { get; set; } = DefaultMaxItems;
    public bool MoveChosenToTop { get; set; }
    public bool AutoInsert { get; set; }
    public bool StartWithWindows { get; set; }
    /// <summary>Hover hint on text rows. Shown only when the row preview was truncated.</summary>
    public bool ShowTextHints { get; set; } = true;
    /// <summary>Hover hint (full-size preview) on image rows.</summary>
    public bool ShowImageHints { get; set; } = true;
    /// <summary>Prefix popup rows with their position ("01.", "02.", ...).</summary>
    public bool ShowNumbers { get; set; } = true;
    public Hotkey PopupHotkey { get; set; } = DefaultPopupHotkey();
    public List<Template> Templates { get; set; } = new();

    public static Hotkey DefaultPopupHotkey() => new(HotkeyModifiers.Control | HotkeyModifiers.Shift, VkV);

    public static int ClampMaxItems(int value) => Math.Clamp(value, MinItems, MaxItemsLimit);

    public Settings Clone() => new()
    {
        MaxItems = MaxItems,
        MoveChosenToTop = MoveChosenToTop,
        AutoInsert = AutoInsert,
        StartWithWindows = StartWithWindows,
        ShowTextHints = ShowTextHints,
        ShowImageHints = ShowImageHints,
        ShowNumbers = ShowNumbers,
        PopupHotkey = new Hotkey(PopupHotkey.Modifiers, PopupHotkey.VirtualKey),
        Templates = Templates.Select(t => t.Clone()).ToList(),
    };

    /// <summary>Returns a human-readable problem, or null when the settings are consistent.</summary>
    public string? Validate()
    {
        if (MaxItems is < MinItems or > MaxItemsLimit)
            return $"Number of stored items must be between {MinItems} and {MaxItemsLimit}.";
        if (PopupHotkey.IsEmpty)
            return "The history hotkey must be set.";

        var seen = new HashSet<Hotkey> { PopupHotkey };
        foreach (var t in Templates)
        {
            if (string.IsNullOrWhiteSpace(t.Name))
                return "Every template needs a name.";
            if (t.Hotkey is { IsEmpty: false } hk && !seen.Add(hk))
                return $"Hotkey {hk} is assigned more than once.";
        }
        return null;
    }
}
