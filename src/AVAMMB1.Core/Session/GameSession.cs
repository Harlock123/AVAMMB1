using AVAMMB1.Core.Characters;
using AVAMMB1.Core.Combat;
using AVAMMB1.Core.Content;
using AVAMMB1.Core.Dice;
using AVAMMB1.Core.Items;
using AVAMMB1.Core.Magic;
using AVAMMB1.Core.Rules;
using AVAMMB1.Core.World;

namespace AVAMMB1.Core.Session;

/// <summary>Relative movement commands.</summary>
public enum MoveKind
{
    /// <summary>Step in the facing direction.</summary>
    Forward,
    /// <summary>Step backwards.</summary>
    Back,
    /// <summary>Side-step left.</summary>
    StrafeLeft,
    /// <summary>Side-step right.</summary>
    StrafeRight,
}

/// <summary>What happened as a result of a player command while exploring.</summary>
public sealed class StepResult
{
    /// <summary>Narration produced.</summary>
    public List<GameMessage> Messages { get; } = new();
    /// <summary>Whether the party changed cells.</summary>
    public bool Moved { get; set; }
    /// <summary>Whether the party changed maps.</summary>
    public bool MapChanged { get; set; }
    /// <summary>A building or service the UI should open.</summary>
    public MapEventDef? Interaction { get; set; }
    /// <summary>A battle that has just begun (see <see cref="GameSession.Combat"/>).</summary>
    public bool CombatStarted { get; set; }
    /// <summary>Story text the UI should present prominently.</summary>
    public string? StoryText { get; set; }
    /// <summary>Title for <see cref="StoryText"/>.</summary>
    public string? StoryTitle { get; set; }
    /// <summary>The game has been won.</summary>
    public bool Victory { get; set; }
    /// <summary>A daily challenge party has climbed out of the Depths: the run is over.</summary>
    public bool ChallengeOver { get; set; }
}

/// <summary>
/// The running game: owns the <see cref="GameState"/> and implements exploration, events,
/// random encounters, resting and the transitions in and out of combat.
/// </summary>
public sealed partial class GameSession
{
    private string? _combatEventKey;

    /// <summary>Creates a session.</summary>
    /// <param name="content">Loaded game content.</param>
    /// <param name="rng">Random source.</param>
    public GameSession(ContentDatabase content, IRandomSource rng)
    {
        Content = content;
        Random = rng;
        Rules = new Rulebook(content);
        Inventory = new Inventory(Rules);
        Spells = new SpellCaster(Rules, rng) { Suppressed = () => IsAntiMagicHere };
        Factory = new CharacterFactory(Rules);
        Town = new TownServices(this);
    }

    /// <summary>Game content.</summary>
    public ContentDatabase Content { get; }
    /// <summary>Random source.</summary>
    public IRandomSource Random { get; }
    /// <summary>Rules.</summary>
    public Rulebook Rules { get; }
    /// <summary>Inventory operations.</summary>
    public Inventory Inventory { get; }
    /// <summary>Spell and item use.</summary>
    public SpellCaster Spells { get; }
    /// <summary>Character creation.</summary>
    public CharacterFactory Factory { get; }
    /// <summary>Town services (inn, temple, shops...).</summary>
    public TownServices Town { get; }
    /// <summary>Current state.</summary>
    public GameState State { get; private set; } = new();
    /// <summary>The battle in progress, if any.</summary>
    public CombatEngine? Combat { get; private set; }
    /// <summary>Whether a game is loaded.</summary>
    public bool IsActive => State.Party.Count > 0 && !string.IsNullOrEmpty(State.MapId);

    /// <summary>The current map.</summary>
    public GameMap CurrentMap => Content.Map(State.MapId);

    /// <summary>Adds everything the party and the inn roster carry to the item compendium.</summary>
    public void NoteCarriedItems()
    {
        foreach (var c in State.Party.Concat(State.Roster))
        {
            foreach (var i in c.Backpack.Concat(c.Equipment.Values))
            {
                State.SeenItems.Add(i.ItemId);
            }
        }
    }

    /// <summary>Adds items to the item compendium (e.g. a shop's stock).</summary>
    /// <param name="itemIds">Item ids.</param>
    public void NoteSeenItems(IEnumerable<string> itemIds)
    {
        foreach (var id in itemIds.Where(Content.Items.ContainsKey))
        {
            State.SeenItems.Add(id);
        }
    }

    /// <summary>Explored maps where a monster can be met (wandering, at night, or standing guard).</summary>
    /// <param name="monsterId">Monster id.</param>
    public IReadOnlyList<string> WhereFound(string monsterId) =>
        Content.Maps.Values
            .Where(m => State.Explored.ContainsKey(m.Id)
                && (m.Def.Encounters.Concat(m.Def.NightEncounters).Any(e => e.Monster == monsterId)
                    || m.AllEvents.Any(ev => ev.Monsters.Any(fm => fm.Monster == monsterId))))
            .Select(m => m.Def.Name)
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToList();

    /// <summary>The level of a daily challenge party.</summary>
    public const int DailyChallengeLevel = 15;

    /// <summary>
    /// Starts a daily challenge: the given party at level 15, with a smith's +2 on its gear, an
    /// everburning lantern and potions, straight into level 1 of that day's Depths Below. It is an
    /// ironman run: one self-kept save, and a wipe ends it.
    /// </summary>
    /// <param name="date">The day ("2026-10-10").</param>
    /// <param name="party">The party.</param>
    public void StartDailyChallenge(string date, IEnumerable<Character> party)
    {
        var (ironman, difficulty, survival) = (Ironman, Difficulty, Survival);
        (Ironman, Difficulty, Survival) = (true, Difficulty.Normal, false); // the same rules for everyone
        NewGame(party);
        (Ironman, Difficulty, Survival) = (ironman, difficulty, survival);
        State.DailyChallenge = date;
        State.Flags.Add(World.Depths.OpenFlag);
        foreach (var c in State.Party)
        {
            c.Experience = Rulebook.XpForLevel(Content.Class(c.Class), DailyChallengeLevel);
            while (Rules.LevelUp(c, Random) is not null)
            {
            }
            foreach (var item in c.Equipment.Values.Where(i => Rulebook.Upgradable(Rules.Def(i))))
            {
                item.Plus = 2;
            }
            c.Backpack.AddRange([new ItemInstance("potion_vigor"), new ItemInstance("potion_vigor"), new ItemInstance("potion_cure")]);
            c.Hp = c.MaxHp;
            c.Sp = c.MaxSp;
            c.Food = Content.Config.MaxFood;
        }
        State.Party[0].Equipment[EquipSlot.Light] = new ItemInstance("lantern_everburning");
        State.MapId = World.Depths.MapId;
        EnterDepth(1, []);
        Explore();
    }

    /// <summary>Generates and enters a level of the Depths Below (a fresh level each descent).</summary>
    /// <param name="depth">Depth (1 and up).</param>
    /// <param name="log">Messages.</param>
    private void EnterDepth(int depth, List<GameMessage> log)
    {
        State.Depth = depth;
        State.DepthSeed = State.DailyChallenge is { } date ? World.Depths.DailySeed(date, depth) : Random.Next(1, int.MaxValue);
        Content.SetGeneratedMap(World.Depths.Generate(Content, depth, State.DepthSeed));
        State.Explored.Remove(World.Depths.MapId); // a new level: nothing mapped yet
        foreach (var key in State.MapNotes.Keys.Where(k => k.StartsWith(World.Depths.MapId + ":", StringComparison.Ordinal)).ToList())
        {
            State.MapNotes.Remove(key);
        }
        (State.X, State.Y) = World.Depths.Start;
        State.Facing = Direction.North;
        if (depth > State.DeepestDepth)
        {
            State.DeepestDepth = depth;
            if (depth > 1)
            {
                log.Add(new($"The deepest the party has ever been: level {depth} of the Depths Below.", MessageKind.Good));
            }
        }
    }

    /// <summary>Moves a party member to another place in the marching order (not during a battle).</summary>
    /// <param name="from">Current index.</param>
    /// <param name="to">New index; the members in between shift along.</param>
    /// <returns>Whether the order changed.</returns>
    public bool MoveMember(int from, int to)
    {
        var party = State.Party;
        if (Combat is not null || from == to || from < 0 || to < 0 || from >= party.Count || to >= party.Count)
        {
            return false;
        }
        var c = party[from];
        party.RemoveAt(from);
        party.Insert(to, c);
        return true;
    }

    /// <summary>
    /// How many cells the party can see. In dark places this is the light radius: torch 5, light
    /// spells 6, lantern 8; with no light, 1.
    /// </summary>
    public int ViewDistance => IsDarkHere ? 1
        : CurrentMap.Def.Kind == MapKind.Outdoor ? (IsNightOutside ? Math.Max(4, LightRadius) : 10)
        : CurrentMap.Def.Kind == MapKind.Town && IsNightOutside ? Math.Max(6, LightRadius)
        : CurrentMap.Def.Dark ? LightRadius : 7;

    /// <summary>Whether it is night under the open sky (outdoors or in a town).</summary>
    public bool IsNightOutside => State.IsNight && CurrentMap.Def.Kind is MapKind.Outdoor or MapKind.Town;

    /// <summary>Reach of the party's light: the brighter of a lit lantern and any torch or spell light (0 = none).</summary>
    public int LightRadius => Math.Max(State.TemporaryLightRadius,
        Items.Lanterns.Active(Rules, State.Party) is { } a ? Rules.Def(a.Lantern).LightRadius : 0);

    /// <summary>Whether the lantern (rather than a torch or spell) is what lights the party now.</summary>
    public bool LanternLit => Items.Lanterns.Active(Rules, State.Party) is { } a && Rules.Def(a.Lantern).LightRadius >= State.TemporaryLightRadius;

    /// <summary>Whether the party is in the dark: an unlit dark map, or a magical-darkness square (where no light helps).</summary>
    public bool IsDarkHere => (CurrentMap.Def.Dark && LightRadius <= 0) || CurrentMap.IsDarkness(State.X, State.Y);

    /// <summary>Whether magic is suppressed where the party stands.</summary>
    public bool IsAntiMagicHere => IsActive && CurrentMap.IsAntiMagic(State.X, State.Y);

    /// <summary>Starts a new game with the given party.</summary>
    /// <param name="party">Party members (1-6).</param>
    /// <param name="roster">Additional characters left at the inn.</param>
    /// <exception cref="ArgumentException">Thrown for an empty or oversized party.</exception>
    public void NewGame(IEnumerable<Character> party, IEnumerable<Character>? roster = null)
    {
        var members = party.ToList();
        if (members.Count is 0 or > GameState.MaxPartySize)
        {
            throw new ArgumentException("A party needs between one and six members.", nameof(party));
        }
        var cfg = Content.Config;
        State = new GameState
        {
            Party = members,
            Roster = roster?.ToList() ?? new List<Character>(),
            MapId = cfg.StartMap,
            X = cfg.StartX,
            Y = cfg.StartY,
            Facing = cfg.StartFacing,
            RecallMap = cfg.StartMap,
            RecallX = cfg.StartX,
            RecallY = cfg.StartY,
        };
        State.PoolAll(); // a new party starts by pooling its gold into the purse
        TestPlaying = false;
        State.Difficulty = Difficulty;
        State.Survival = Survival;
        State.Ironman = Ironman;
        Combat = null;
        Explore();
    }

    /// <summary>Whether the running game is a map editor test play (nothing is saved or recorded).</summary>
    public bool TestPlaying { get; private set; }

    /// <summary>
    /// Starts a map editor test play: the given party (at the given level) on an edited map, placed at a
    /// square. Nothing is saved; call <see cref="EndTestPlay"/> afterwards.
    /// </summary>
    /// <param name="map">The edited map (already put in place with <see cref="ContentDatabase.UseEditedMap"/>).</param>
    /// <param name="party">The party.</param>
    /// <param name="x">Start X.</param>
    /// <param name="y">Start Y.</param>
    /// <param name="level">Party level.</param>
    public void StartTestPlay(string map, IEnumerable<Character> party, int x, int y, int level = 1)
    {
        NewGame(party);
        foreach (var c in State.Party.Where(_ => level > 1))
        {
            c.Experience = Rulebook.XpForLevel(Content.Class(c.Class), level);
            while (Rules.LevelUp(c, Random) is not null)
            {
            }
            c.Hp = c.MaxHp;
            c.Sp = c.MaxSp;
        }
        State.Party[0].Equipment[EquipSlot.Light] = new ItemInstance("lantern_everburning"); // to see dark maps
        (State.MapId, State.X, State.Y, State.Facing) = (map, x, y, Direction.North);
        TestPlaying = true;
        Explore();
    }

    /// <summary>Ends a test play.</summary>
    public void EndTestPlay()
    {
        TestPlaying = false;
        Combat = null;
    }

    /// <summary>Difficulty for the next new game.</summary>
    public Difficulty Difficulty { get; set; } = Difficulty.Normal;

    /// <summary>Survival mode for the next new game.</summary>
    public bool Survival { get; set; }

    /// <summary>Ironman for the next new game.</summary>
    public bool Ironman { get; set; }

    /// <summary>Replaces the state with a loaded one.</summary>
    /// <param name="state">Loaded state.</param>
    /// <exception cref="InvalidDataException">Thrown when the state references unknown content.</exception>
    public void Load(GameState state)
    {
        if (state.MapId == World.Depths.MapId && state.Depth > 0)
        {
            Content.SetGeneratedMap(World.Depths.Generate(Content, state.Depth, state.DepthSeed)); // the saved level, exactly
        }
        if (!Content.Maps.TryGetValue(state.MapId, out var map) || !map.InBounds(state.X, state.Y))
        {
            throw new InvalidDataException($"Saved location '{state.MapId}' ({state.X},{state.Y}) is not valid for this content.");
        }
        foreach (var c in state.Party.Concat(state.Roster))
        {
            if (!Content.Classes.ContainsKey(c.Class) || !Content.Races.ContainsKey(c.Race))
            {
                throw new InvalidDataException($"Character '{c.Name}' uses unknown race or class.");
            }
            c.Backpack.RemoveAll(i => !Content.Items.ContainsKey(i.ItemId));
            foreach (var slot in c.Equipment.Where(kv => !Content.Items.ContainsKey(kv.Value.ItemId)).Select(kv => kv.Key).ToList())
            {
                c.Equipment.Remove(slot);
            }
        }
        State = state;
        if (State.Minutes == 0 && State.Steps > 0)
        {
            State.Minutes = State.Steps * GameState.MinutesPerStep; // saves from before the clock
        }
        if (State.LastMealMinutes < State.Minutes - GameState.MinutesPerDay)
        {
            State.LastMealMinutes = State.Minutes; // saves from before survival mode: start the day fed
        }
        Chronicle.Check(this); // achievements already met by an older save are recorded quietly
        Combat = null;
        TestPlaying = false;
        Explore();
    }

    /// <summary>Short location description for save files and the status bar.</summary>
    public string LocationSummary => $"{CurrentMap.Def.Name} ({State.X},{State.Y}) - Day {State.Day}";
}
