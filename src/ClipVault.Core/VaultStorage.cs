using System.Text.Json;

namespace ClipVault.Core;

public sealed class HistoryData
{
    public List<Clip> Items { get; set; } = new();
    public Guid? LastChosenId { get; set; }
}

/// <summary>
/// On-disk layout under the root directory:
/// settings.json, history.json, images\{clip id}.png
/// </summary>
public sealed class VaultStorage
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    private readonly string _settingsFile;
    private readonly string _historyFile;

    public VaultStorage(string rootDir)
    {
        RootDir = rootDir;
        ImagesDir = Path.Combine(rootDir, "images");
        _settingsFile = Path.Combine(rootDir, "settings.json");
        _historyFile = Path.Combine(rootDir, "history.json");
        Directory.CreateDirectory(ImagesDir);
    }

    public string RootDir { get; }
    public string ImagesDir { get; }

    public static string DefaultRoot() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClipVault");

    public Settings LoadSettings() => Load<Settings>(_settingsFile) ?? new Settings();

    public void SaveSettings(Settings settings) => Save(_settingsFile, settings);

    public HistoryData LoadHistory()
    {
        var data = Load<HistoryData>(_historyFile) ?? new HistoryData();
        // Drop image clips whose file vanished.
        data.Items.RemoveAll(c => c.Kind == ClipKind.Image && !File.Exists(ImagePath(c)));
        return data;
    }

    public void SaveHistory(History history) => Save(_historyFile, new HistoryData
    {
        Items = history.Items.ToList(),
        LastChosenId = history.LastChosenId,
    });

    public string ImagePath(Clip clip) => Path.Combine(ImagesDir, clip.ImageFile!);

    /// <summary>Writes the PNG bytes and returns the file name to store on the clip.</summary>
    public string SaveImage(Guid id, byte[] png)
    {
        var name = id.ToString("N") + ".png";
        File.WriteAllBytes(Path.Combine(ImagesDir, name), png);
        return name;
    }

    public void DeleteImage(Clip clip)
    {
        if (clip.Kind != ClipKind.Image) return;
        try { File.Delete(ImagePath(clip)); } catch (IOException) { }
    }

    /// <summary>Deletes image files no clip refers to.</summary>
    public void DeleteOrphanImages(IEnumerable<Clip> clips)
    {
        var referenced = clips.Where(c => c.ImageFile != null).Select(c => c.ImageFile!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFiles(ImagesDir, "*.png"))
        {
            if (!referenced.Contains(Path.GetFileName(file)))
                try { File.Delete(file); } catch (IOException) { }
        }
    }

    private static T? Load<T>(string file) where T : class
    {
        if (!File.Exists(file)) return null;
        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(file), Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void Save<T>(string file, T value)
    {
        // Write to a temp file and swap so a crash mid-write never leaves a truncated file.
        var tmp = file + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(value, Json));
        File.Move(tmp, file, overwrite: true);
    }
}
