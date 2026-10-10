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
    /// <summary>The party purse: gold shared by everyone in the party (loot goes here).</summary>
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
    /// <summary>Secret doors the party has discovered (see <see cref="SecretKey"/>).</summary>
    public HashSet<string> FoundSecrets { get; set; } = new(StringComparer.Ordinal);
    /// <summary>Locked doors a robber has picked (keys as <see cref="SecretKey"/>).</summary>
    public HashSet<string> PickedLocks { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Monster kinds the party has defeated; the combat screen shows their statistics.</summary>
    public HashSet<string> KnownMonsters { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Monsters slain, by monster id (the bestiary and the chronicle).</summary>
    public Dictionary<string, int> Kills { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Statistics for the Chronicle (see <see cref="Chronicle.Keys"/>).</summary>
    public Dictionary<string, long> Stats { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Achievements earned in this game.</summary>
    public HashSet<string> Achievements { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Adds to a statistic.</summary>
    /// <param name="key">Statistic (see <see cref="Chronicle.Keys"/>).</param>
    /// <param name="by">Amount.</param>
    public void Count(string key, long by = 1) => Stats[key] = Stats.GetValueOrDefault(key) + by;

    /// <summary>Items the party has carried or seen for sale (the item compendium).</summary>
    public HashSet<string> SeenItems { get; set; } = new(StringComparer.Ordinal);
    /// <summary>Player notes on automap squares, keyed by <see cref="NoteKey"/>.</summary>
    public Dictionary<string, string> MapNotes { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Key for <see cref="MapNotes"/>.</summary>
    /// <param name="mapId">Map.</param>
    /// <param name="x">X.</param>
    /// <param name="y">Y.</param>
    public static string NoteKey(string mapId, int x, int y) => $"{mapId}:{x}:{y}";

    /// <summary>Sets (or, with empty text, removes) the note on a square.</summary>
    /// <param name="mapId">Map.</param>
    /// <param name="x">X.</param>
    /// <param name="y">Y.</param>
    /// <param name="text">Note text.</param>
    public void SetNote(string mapId, int x, int y, string? text)
    {
        var key = NoteKey(mapId, x, y);
        if (string.IsNullOrWhiteSpace(text))
        {
            MapNotes.Remove(key);
        }
        else
        {
            MapNotes[key] = text.Trim();
        }
    }

    /// <summary>The note on a square, if any.</summary>
    /// <param name="mapId">Map.</param>
    /// <param name="x">X.</param>
    /// <param name="y">Y.</param>
    public string? NoteAt(string mapId, int x, int y) => MapNotes.GetValueOrDefault(NoteKey(mapId, x, y));

    /// <summary>Total steps taken (game clock).</summary>
    public long Steps { get; set; }
    /// <summary>Remaining steps of magical or torch light.</summary>
    public int LightSteps { get; set; }
    /// <summary>Radius of the current torch or spell light (0 in saves from before 1.8 = the old 6).</summary>
    public int LightRadius { get; set; }

    /// <summary>Radius of the torch/spell light while it lasts, else 0.</summary>
    public int TemporaryLightRadius => LightSteps > 0 ? (LightRadius > 0 ? LightRadius : 6) : 0;

    /// <summary>Adds torch or spell light; the brighter of the old and new light is kept.</summary>
    /// <param name="steps">Steps of light.</param>
    /// <param name="radius">Radius in squares.</param>
    public void AddLight(int steps, int radius)
    {
        LightRadius = Math.Max(TemporaryLightRadius, radius);
        LightSteps += steps;
    }
    /// <summary>Map the Recall spell returns to.</summary>
    public string RecallMap { get; set; } = "";
    /// <summary>Recall X.</summary>
    public int RecallX { get; set; }
    /// <summary>Recall Y.</summary>
    public int RecallY { get; set; }
    /// <summary>Whether the final objective has been completed.</summary>
    public bool Won { get; set; }

    /// <summary>Purse plus the personal gold of every party member.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public int TotalGold => Gold + Party.Sum(c => c.Gold);

    /// <summary>
    /// Gold available for a payment: the purse plus the payer's own gold, or plus everyone's gold
    /// for whole-party costs (<paramref name="payer"/> null).
    /// </summary>
    /// <param name="payer">The character being served, or null for party-wide costs.</param>
    public int Available(Characters.Character? payer) => Gold + (payer?.Gold ?? Party.Sum(c => c.Gold));

    /// <summary>
    /// Pays a cost from the purse first, then from the payer's own gold (or, for party-wide costs,
    /// from members' gold in marching order). Nothing is taken if the total is not enough.
    /// </summary>
    /// <param name="cost">Gold to pay.</param>
    /// <param name="payer">The character being served, or null for party-wide costs.</param>
    /// <returns>True when paid.</returns>
    public bool TryPay(int cost, Characters.Character? payer = null)
    {
        if (cost <= 0)
        {
            return true;
        }
        if (Available(payer) < cost)
        {
            return false;
        }
        var fromPurse = Math.Min(Gold, cost);
        Gold -= fromPurse;
        cost -= fromPurse;
        foreach (var c in payer is null ? Party : [payer])
        {
            var take = Math.Min(c.Gold, cost);
            c.Gold -= take;
            cost -= take;
            if (cost == 0)
            {
                break;
            }
        }
        return true;
    }

    /// <summary>Moves gold from a character into the purse.</summary>
    /// <param name="c">Character.</param>
    /// <param name="amount">Amount (clamped to what the character carries).</param>
    /// <returns>Amount moved.</returns>
    public int Deposit(Characters.Character c, int amount)
    {
        var moved = Math.Clamp(amount, 0, c.Gold);
        c.Gold -= moved;
        Gold += moved;
        return moved;
    }

    /// <summary>Moves gold from the purse to a character.</summary>
    /// <param name="c">Character.</param>
    /// <param name="amount">Amount (clamped to what the purse holds).</param>
    /// <returns>Amount moved.</returns>
    public int Withdraw(Characters.Character c, int amount)
    {
        var moved = Math.Clamp(amount, 0, Gold);
        Gold -= moved;
        c.Gold += moved;
        return moved;
    }

    /// <summary>Everyone puts all their gold into the purse.</summary>
    /// <returns>Amount pooled.</returns>
    public int PoolAll() => Party.Sum(c => Deposit(c, c.Gold));

    /// <summary>Splits the purse evenly among party members (any remainder stays in the purse).</summary>
    /// <returns>Amount each member received.</returns>
    public int ShareEvenly()
    {
        if (Party.Count == 0)
        {
            return 0;
        }
        var each = Gold / Party.Count;
        foreach (var c in Party)
        {
            Withdraw(c, each);
        }
        return each;
    }

    /// <summary>Game day derived from the step counter.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public long Day => 1 + (Minutes + StartMinuteOfDay) / MinutesPerDay;

    /// <summary>Minutes in a day.</summary>
    public const int MinutesPerDay = 1440;
    /// <summary>Game minutes per step.</summary>
    public const int MinutesPerStep = 3;
    /// <summary>The adventure starts at 08:00.</summary>
    public const int StartMinuteOfDay = 8 * 60;

    /// <summary>The game clock: minutes since the adventure began.</summary>
    public long Minutes { get; set; }

    /// <summary>Seconds of real time played (counted by the app; shown with saves).</summary>
    public long PlaySeconds { get; set; }

    /// <summary>Difficulty of this game.</summary>
    public Difficulty Difficulty { get; set; } = Difficulty.Normal;

    /// <summary>Ironman: the game keeps a single save itself, and a party wipe ends the run.</summary>
    public bool Ironman { get; set; }

    /// <summary>Survival mode: besides resting, everyone eats one food a day, and goes hungry without.</summary>
    public bool Survival { get; set; }

    /// <summary>Clock time of the party's last meal (a rest, a night at an inn, or a daily ration).</summary>
    public long LastMealMinutes { get; set; }

    /// <summary>Minutes since midnight (0-1439).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public int MinuteOfDay => (int)((Minutes + StartMinuteOfDay) % MinutesPerDay);

    /// <summary>The time as hh:mm.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string ClockText => $"{MinuteOfDay / 60:00}:{MinuteOfDay % 60:00}";

    /// <summary>Night is 20:00 to 05:00.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsNight => MinuteOfDay >= 20 * 60 || MinuteOfDay < 5 * 60;

    /// <summary>How dark the sky is: 0 by day, 1 at night, in between at dusk (18-20) and dawn (05-07).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public double Darkness => MinuteOfDay switch
    {
        >= 7 * 60 and < 18 * 60 => 0,
        >= 18 * 60 and < 20 * 60 => (MinuteOfDay - 18 * 60) / 120.0,
        >= 5 * 60 and < 7 * 60 => 1 - (MinuteOfDay - 5 * 60) / 120.0,
        _ => 1,
    };

    /// <summary>"night", "dawn", "day" or "dusk".</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string PartOfDay => MinuteOfDay switch
    {
        >= 5 * 60 and < 7 * 60 => "dawn",
        >= 7 * 60 and < 18 * 60 => "day",
        >= 18 * 60 and < 20 * 60 => "dusk",
        _ => "night",
    };

    /// <summary>Moves the clock forward to the next given time of day.</summary>
    /// <param name="minuteOfDay">Target time (minutes since midnight).</param>
    public void AdvanceTo(int minuteOfDay)
    {
        var wait = (minuteOfDay - MinuteOfDay + MinutesPerDay) % MinutesPerDay;
        Minutes += wait == 0 ? MinutesPerDay : wait;
    }

    /// <summary>
    /// Canonical key for the edge on one side of a cell, so both cells sharing a wall
    /// produce the same key (east/south edges are stored as the neighbor's west/north edge).
    /// </summary>
    /// <param name="mapId">Map id.</param>
    /// <param name="x">Cell X.</param>
    /// <param name="y">Cell Y.</param>
    /// <param name="side">Side of the cell.</param>
    public static string SecretKey(string mapId, int x, int y, Direction side) => side switch
    {
        Direction.East => $"{mapId}:{x + 1}:{y}:W",
        Direction.South => $"{mapId}:{x}:{y + 1}:N",
        Direction.West => $"{mapId}:{x}:{y}:W",
        _ => $"{mapId}:{x}:{y}:N",
    };

    /// <summary>Whether the secret door on a side of a cell has been found.</summary>
    /// <param name="mapId">Map id.</param>
    /// <param name="x">Cell X.</param>
    /// <param name="y">Cell Y.</param>
    /// <param name="side">Side of the cell.</param>
    public bool IsSecretFound(string mapId, int x, int y, Direction side) => FoundSecrets.Contains(SecretKey(mapId, x, y, side));

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
