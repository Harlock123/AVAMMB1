using AVAMMB1.Core.Characters;
using AVAMMB1.Core.Rules;

namespace AVAMMB1.Core.Session;

/// <summary>All mutable state of a game in progress. Serialized to JSON by save games.</summary>
public sealed class GameState
{
    /// <summary>Maximum party size.</summary>
    public const int MaxPartySize = 6;

    /// <summary>Maximum number of characters waiting at the inn.</summary>
    public const int MaxRosterSize = 18;

    /// <summary>Active party members in marching order (first three form the front rank).</summary>
    public List<Character> Party { get; set; } = new();
    /// <summary>Characters resting at the inn (not in the party).</summary>
    public List<Character> Roster { get; set; } = new();
    /// <summary>Shared party gold.</summary>
    public int Gold { get; set; }
    /// <summary>Shared party gems.</summary>
    public int Gems { get; set; }
    /// <summary>Current map id.</summary>
    public string MapId { get; set; } = "";
    /// <summary>Current X.</summary>
    public int X { get; set; }
    /// <summary>Current Y.</summary>
    public int Y { get; set; }
    /// <summary>Current facing.</summary>
    public Direction Facing { get; set; }
    /// <summary>Quest flags that have been set.</summary>
    public HashSet<string> Flags { get; set; } = new(StringComparer.Ordinal);
    /// <summary>Keys of one-shot events that already fired.</summary>
    public HashSet<string> CompletedEvents { get; set; } = new(StringComparer.Ordinal);
    /// <summary>Explored cells per map, as a string of '0'/'1' (row-major).</summary>
    public Dictionary<string, string> Explored { get; set; } = new(StringComparer.Ordinal);
    /// <summary>Total steps taken (game clock).</summary>
    public long Steps { get; set; }
    /// <summary>Remaining steps of magical or torch light.</summary>
    public int LightSteps { get; set; }
    /// <summary>Map the Recall spell returns to.</summary>
    public string RecallMap { get; set; } = "";
    /// <summary>Recall X.</summary>
    public int RecallX { get; set; }
    /// <summary>Recall Y.</summary>
    public int RecallY { get; set; }
    /// <summary>Whether the final objective has been completed.</summary>
    public bool Won { get; set; }

    /// <summary>Game day derived from the step counter.</summary>
    public long Day => 1 + Steps / 500;

    /// <summary>Marks a cell explored.</summary>
    /// <param name="mapId">Map id.</param>
    /// <param name="width">Map width.</param>
    /// <param name="height">Map height.</param>
    /// <param name="x">Cell X.</param>
    /// <param name="y">Cell Y.</param>
    public void MarkExplored(string mapId, int width, int height, int x, int y)
    {
        if (x < 0 || y < 0 || x >= width || y >= height)
        {
            return;
        }
        if (!Explored.TryGetValue(mapId, out var bits) || bits.Length != width * height)
        {
            bits = new string('0', width * height);
        }
        var idx = y * width + x;
        if (bits[idx] == '1')
        {
            return;
        }
        Explored[mapId] = string.Concat(bits.AsSpan(0, idx), "1", bits.AsSpan(idx + 1));
    }

    /// <summary>Whether a cell has been explored.</summary>
    /// <param name="mapId">Map id.</param>
    /// <param name="width">Map width.</param>
    /// <param name="x">Cell X.</param>
    /// <param name="y">Cell Y.</param>
    public bool IsExplored(string mapId, int width, int x, int y)
    {
        if (!Explored.TryGetValue(mapId, out var bits))
        {
            return false;
        }
        var idx = y * width + x;
        return x >= 0 && y >= 0 && x < width && idx < bits.Length && bits[idx] == '1';
    }
}
