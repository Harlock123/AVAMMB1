using System.Text.Json;
using System.Text.Json.Nodes;
using AVAMMB1.Core.Session;

namespace AVAMMB1.Core.Persistence;

/// <summary>On-disk save file wrapper.</summary>
public sealed class SaveFile
{
    /// <summary>
    /// Current format version. History: 1 = AVAM&amp;M 1.0-1.4 (computed values were also written);
    /// 2 = 1.5 onwards (computed values dropped, <see cref="GameVersion"/> recorded).
    /// </summary>
    public const int CurrentVersion = 2;

    /// <summary>Format version.</summary>
    public int Version { get; set; } = CurrentVersion;
    /// <summary>Version of the game that wrote the file (informational).</summary>
    public string GameVersion { get; set; } = typeof(SaveFile).Assembly.GetName().Version?.ToString(3) ?? "";
    /// <summary>User supplied or automatic name.</summary>
    public string Name { get; set; } = "";
    /// <summary>When the game was saved (UTC).</summary>
    public DateTime SavedUtc { get; set; }
    /// <summary>Short description (location, party).</summary>
    public string Summary { get; set; } = "";
    /// <summary>The game state.</summary>
    public GameState State { get; set; } = new();
}

/// <summary>Metadata about a save slot.</summary>
/// <param name="Slot">Slot number.</param>
/// <param name="Exists">Whether the slot holds a save.</param>
/// <param name="Name">Save name.</param>
/// <param name="SavedUtc">Save time.</param>
/// <param name="Summary">Description.</param>
public sealed record SaveSlotInfo(int Slot, bool Exists, string Name, DateTime SavedUtc, string Summary);

/// <summary>Saves and loads games as JSON files in a directory.</summary>
public sealed class SaveGameService
{
    /// <summary>Number of manual save slots (slot 0 is the quick-save slot).</summary>
    public const int SlotCount = 10;

    /// <summary>Creates the service.</summary>
    /// <param name="directory">Directory holding save files (created if missing).</param>
    public SaveGameService(string directory)
    {
        Directory = directory;
    }

    /// <summary>The save directory.</summary>
    public string Directory { get; }

    private string PathFor(int slot) => Path.Combine(Directory, $"slot{slot}.json");

    /// <summary>Serializes a state to a JSON string.</summary>
    /// <param name="file">Save file.</param>
    public static string Serialize(SaveFile file) => JsonSerializer.Serialize(file, GameJsonContext.Default.SaveFile);

    /// <summary>Deserializes a save file from JSON, upgrading older formats first (see <see cref="SaveMigrations"/>).</summary>
    /// <param name="json">JSON text.</param>
    /// <exception cref="InvalidDataException">Thrown for corrupt or unsupported files.</exception>
    public static SaveFile Deserialize(string json)
    {
        SaveFile? file;
        try
        {
            var node = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { AllowTrailingCommas = true }) as JsonObject
                ?? throw new InvalidDataException("The save file is corrupt.");
            var version = SaveMigrations.VersionOf(node);
            if (version > SaveFile.CurrentVersion)
            {
                var by = node["gameVersion"]?.GetValue<string>();
                throw new InvalidDataException($"The save file is from a newer version of the game{(by is null ? "" : $" ({by})")}. Please update AVAM&M.");
            }
            SaveMigrations.Upgrade(node);
            file = node.Deserialize(GameJsonContext.Default.SaveFile);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("The save file is corrupt.", ex);
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidDataException("The save file is corrupt.", ex);
        }
        return file ?? throw new InvalidDataException("The save file is empty.");
    }

    /// <summary>Writes a save to a slot (atomically).</summary>
    /// <param name="slot">Slot number.</param>
    /// <param name="name">Save name.</param>
    /// <param name="summary">Description.</param>
    /// <param name="state">State to save.</param>
    public void Save(int slot, string name, string summary, GameState state)
    {
        ValidateSlot(slot);
        System.IO.Directory.CreateDirectory(Directory);
        var file = new SaveFile { Name = name, Summary = summary, SavedUtc = DateTime.UtcNow, State = state };
        BackupIfOlderFormat(slot);
        var tmp = PathFor(slot) + ".tmp";
        File.WriteAllText(tmp, Serialize(file));
        File.Move(tmp, PathFor(slot), overwrite: true);
    }

    /// <summary>Before an older-format save is overwritten, keep a copy (e.g. <c>slot1.json.v1.bak</c>).</summary>
    private void BackupIfOlderFormat(int slot)
    {
        var path = PathFor(slot);
        if (!File.Exists(path))
        {
            return;
        }
        try
        {
            var version = JsonNode.Parse(File.ReadAllText(path)) is JsonObject o ? SaveMigrations.VersionOf(o) : SaveFile.CurrentVersion;
            if (version < SaveFile.CurrentVersion && !File.Exists($"{path}.v{version}.bak"))
            {
                File.Copy(path, $"{path}.v{version}.bak");
            }
        }
        catch (JsonException)
        {
            // An unreadable old file is simply replaced.
        }
    }

    /// <summary>Loads the state in a slot.</summary>
    /// <param name="slot">Slot number.</param>
    /// <exception cref="FileNotFoundException">Thrown when the slot is empty.</exception>
    public SaveFile Load(int slot)
    {
        ValidateSlot(slot);
        var path = PathFor(slot);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("That slot is empty.", path);
        }
        return Deserialize(File.ReadAllText(path));
    }

    /// <summary>Whether a slot holds a save.</summary>
    /// <param name="slot">Slot number.</param>
    public bool Exists(int slot) => File.Exists(PathFor(slot));

    /// <summary>Lists all slots.</summary>
    public IReadOnlyList<SaveSlotInfo> List()
    {
        var list = new List<SaveSlotInfo>();
        for (var i = 0; i < SlotCount; i++)
        {
            try
            {
                if (Exists(i))
                {
                    var f = Load(i);
                    list.Add(new SaveSlotInfo(i, true, f.Name, f.SavedUtc, f.Summary));
                    continue;
                }
            }
            catch (InvalidDataException)
            {
                list.Add(new SaveSlotInfo(i, true, "(corrupt save)", DateTime.MinValue, ""));
                continue;
            }
            list.Add(new SaveSlotInfo(i, false, "", DateTime.MinValue, ""));
        }
        return list;
    }

    /// <summary>The most recently written slot, if any.</summary>
    public int? MostRecentSlot() =>
        List().Where(s => s.Exists && s.SavedUtc > DateTime.MinValue).OrderByDescending(s => s.SavedUtc).Select(s => (int?)s.Slot).FirstOrDefault();

    private static void ValidateSlot(int slot)
    {
        if (slot < 0 || slot >= SlotCount)
        {
            throw new ArgumentOutOfRangeException(nameof(slot));
        }
    }
}
