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
    /// <summary>Ids of the mod packs that were active when the game was saved.</summary>
    public List<string> Mods { get; set; } = new();
    /// <summary>The game state.</summary>
    public GameState State { get; set; } = new();
}

/// <summary>Metadata about a save slot.</summary>
/// <param name="Slot">Slot number.</param>
/// <param name="Exists">Whether the slot holds a save.</param>
/// <param name="Name">Save name.</param>
/// <param name="SavedUtc">Save time.</param>
/// <param name="Summary">Description.</param>
/// <param name="PlayTime">Time played (zero for saves from before it was tracked).</param>
/// <param name="ThumbnailPath">Picture of the view when saved, if any.</param>
public sealed record SaveSlotInfo(int Slot, bool Exists, string Name, DateTime SavedUtc, string Summary, TimeSpan PlayTime = default, string? ThumbnailPath = null)
{
    /// <summary>Whether this is one of the rotating autosave slots.</summary>
    public bool IsAuto => Slot >= SaveGameService.SlotCount && Slot < SaveGameService.IronmanSlot;

    /// <summary>Whether this is the ironman slot.</summary>
    public bool IsIronman => Slot == SaveGameService.IronmanSlot;
}

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

    /// <summary>Mod packs active in this session (written into new saves).</summary>
    public IReadOnlyList<string> ActiveMods { get; set; } = [];

    private string PathFor(int slot) => Path.Combine(Directory,
        slot == IronmanSlot ? "ironman.json" : slot >= SlotCount ? $"auto{slot - SlotCount + 1}.json" : $"slot{slot}.json");

    /// <summary>The single slot an ironman game keeps itself (after the autosave slots).</summary>
    public const int IronmanSlot = SlotCount + AutoSlotCount;

    /// <summary>Deletes a save and its picture (an ironman run that has ended).</summary>
    /// <param name="slot">Slot number.</param>
    public void Delete(int slot)
    {
        ValidateSlot(slot);
        foreach (var path in new[] { PathFor(slot), ThumbnailFor(slot) })
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    /// <summary>Number of rotating autosave slots (numbered after the manual ones).</summary>
    public const int AutoSlotCount = 3;

    /// <summary>All slots, manual and automatic.</summary>
    public const int TotalSlots = SlotCount + AutoSlotCount;

    /// <summary>Where a slot's thumbnail picture is kept.</summary>
    /// <param name="slot">Slot number.</param>
    public string ThumbnailFor(int slot) => Path.ChangeExtension(PathFor(slot), ".png");

    /// <summary>Writes an autosave into the oldest (or an empty) autosave slot.</summary>
    /// <param name="summary">Description.</param>
    /// <param name="state">State to save.</param>
    /// <param name="thumbnail">PNG of the view, if any.</param>
    /// <returns>The slot written.</returns>
    public int AutoSave(string summary, GameState state, byte[]? thumbnail = null)
    {
        var slot = Enumerable.Range(SlotCount, AutoSlotCount)
            .OrderBy(s => File.Exists(PathFor(s)) ? File.GetLastWriteTimeUtc(PathFor(s)) : DateTime.MinValue)
            .ThenBy(s => s)
            .First();
        Save(slot, "Autosave", summary, state, thumbnail);
        return slot;
    }

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
    /// <param name="thumbnail">PNG of the view, if any (otherwise an old picture is removed).</param>
    public void Save(int slot, string name, string summary, GameState state, byte[]? thumbnail = null)
    {
        ValidateSlot(slot);
        System.IO.Directory.CreateDirectory(Directory);
        var file = new SaveFile { Name = name, Summary = summary, SavedUtc = DateTime.UtcNow, State = state, Mods = ActiveMods.ToList() };
        BackupIfOlderFormat(slot);
        var tmp = PathFor(slot) + ".tmp";
        File.WriteAllText(tmp, Serialize(file));
        File.Move(tmp, PathFor(slot), overwrite: true);
        var pic = ThumbnailFor(slot);
        if (thumbnail is { Length: > 0 })
        {
            File.WriteAllBytes(pic, thumbnail);
        }
        else if (File.Exists(pic))
        {
            File.Delete(pic);
        }
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
        for (var i = 0; i <= IronmanSlot; i++)
        {
            if (i == IronmanSlot && !Exists(i))
            {
                break; // the ironman slot is listed only when a run is in progress
            }
            try
            {
                if (Exists(i))
                {
                    var f = Load(i);
                    var pic = ThumbnailFor(i);
                    list.Add(new SaveSlotInfo(i, true, f.Name, f.SavedUtc, f.Summary, TimeSpan.FromSeconds(f.State.PlaySeconds), File.Exists(pic) ? pic : null));
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
        if (slot < 0 || slot > IronmanSlot)
        {
            throw new ArgumentOutOfRangeException(nameof(slot));
        }
    }
}
