using System.Text.Json;
using AVAMMB1.Core.Content;
using AVAMMB1.Core.Rules;
using AVAMMB1.Core.Session;

namespace AVAMMB1.Core.Persistence;

/// <summary>One finished run: a victory, or an ironman party that fell.</summary>
public sealed class HallOfFameEntry
{
    /// <summary>When the run ended.</summary>
    public DateTime FinishedUtc { get; set; }
    /// <summary>"Victory" or "Fell in the Rime Halls".</summary>
    public string Outcome { get; set; } = "";
    /// <summary>Whether the main quest was completed.</summary>
    public bool Won { get; set; }
    /// <summary>Difficulty played.</summary>
    public Difficulty Difficulty { get; set; } = Difficulty.Normal;
    /// <summary>Survival mode.</summary>
    public bool Survival { get; set; }
    /// <summary>Ironman mode.</summary>
    public bool Ironman { get; set; }
    /// <summary>Days on the clock.</summary>
    public long Day { get; set; }
    /// <summary>Real time played.</summary>
    public long PlaySeconds { get; set; }
    /// <summary>Monsters slain.</summary>
    public int MonstersSlain { get; set; }
    /// <summary>"Brannoc, Human Knight 14".</summary>
    public List<string> Party { get; set; } = new();
    /// <summary>Achievement ids earned in the run.</summary>
    public List<string> Achievements { get; set; } = new();

    /// <summary>Describes a run as it ends.</summary>
    /// <param name="state">Final state.</param>
    /// <param name="content">Content (names).</param>
    /// <param name="outcome">"Victory" or "Fell in ...".</param>
    public static HallOfFameEntry From(GameState state, ContentDatabase content, string outcome) => new()
    {
        FinishedUtc = DateTime.UtcNow,
        Outcome = outcome,
        Won = state.Won,
        Difficulty = state.Difficulty,
        Survival = state.Survival,
        Ironman = state.Ironman,
        Day = state.Day,
        PlaySeconds = state.PlaySeconds,
        MonstersSlain = state.Kills.Values.Sum(),
        Party = state.Party.Select(c => $"{c.Name}, {(content.Races.TryGetValue(c.Race, out var r) ? r.Name : c.Race)} {(content.Classes.TryGetValue(c.Class, out var k) ? k.Name : c.Class)} {c.Level}").ToList(),
        Achievements = state.Achievements.Order(StringComparer.Ordinal).ToList(),
    };
}

/// <summary>Finished runs and every achievement earned in any game, kept across games.</summary>
public sealed class HallOfFame
{
    /// <summary>Finished runs, newest first.</summary>
    public List<HallOfFameEntry> Entries { get; set; } = new();
    /// <summary>Achievements earned in any game.</summary>
    public HashSet<string> Achievements { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Adds a finished run (newest first; at most 100 are kept).</summary>
    /// <param name="entry">The run.</param>
    public void Add(HallOfFameEntry entry)
    {
        Entries.Insert(0, entry);
        if (Entries.Count > 100)
        {
            Entries.RemoveRange(100, Entries.Count - 100);
        }
        Achievements.UnionWith(entry.Achievements);
    }
}

/// <summary>Reads and writes the Hall of Fame file.</summary>
/// <param name="path">File path.</param>
public sealed class HallOfFameStore(string path)
{
    /// <summary>The file.</summary>
    public string Path { get; } = path;

    /// <summary>Loads the Hall of Fame (empty when missing or unreadable).</summary>
    public HallOfFame Load()
    {
        try
        {
            return File.Exists(Path) ? JsonSerializer.Deserialize(File.ReadAllText(Path), GameJsonContext.Default.HallOfFame) ?? new() : new();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return new();
        }
    }

    /// <summary>Saves it (atomically); failures are ignored - the Hall of Fame is a nicety.</summary>
    /// <param name="hof">Hall of Fame.</param>
    public void Save(HallOfFame hof)
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            var tmp = Path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(hof, GameJsonContext.Default.HallOfFame));
            File.Move(tmp, Path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Not worth interrupting a game over.
        }
    }
}
