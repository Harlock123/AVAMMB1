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
}

/// <summary>
/// The running game: owns the <see cref="GameState"/> and implements exploration, events,
/// random encounters, resting and the transitions in and out of combat.
/// </summary>
public sealed class GameSession
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

    /// <summary>How many cells the party can see (light matters in dark places).</summary>
    public int ViewDistance => IsDarkHere ? 1 : CurrentMap.Def.Kind == MapKind.Outdoor ? 10 : 7;

    /// <summary>Whether the party is in the dark: an unlit dark map, or a magical-darkness square (where no light helps).</summary>
    public bool IsDarkHere => (CurrentMap.Def.Dark && State.LightSteps <= 0) || CurrentMap.IsDarkness(State.X, State.Y);

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
        Combat = null;
        Explore();
    }

    /// <summary>Replaces the state with a loaded one.</summary>
    /// <param name="state">Loaded state.</param>
    /// <exception cref="InvalidDataException">Thrown when the state references unknown content.</exception>
    public void Load(GameState state)
    {
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
        Combat = null;
        Explore();
    }

    /// <summary>Short location description for save files and the status bar.</summary>
    public string LocationSummary => $"{CurrentMap.Def.Name} ({State.X},{State.Y}) - Day {State.Day}";

    /// <summary>Turns the party left.</summary>
    public void TurnLeft() => State.Facing = State.Facing.Left();

    /// <summary>Turns the party right.</summary>
    public void TurnRight() => State.Facing = State.Facing.Right();

    /// <summary>Moves the party.</summary>
    /// <param name="kind">Relative movement.</param>
    public StepResult Move(MoveKind kind)
    {
        var result = new StepResult();
        if (Combat is not null || !IsActive)
        {
            return result;
        }
        if (!State.Party.Any(c => c.CanAct))
        {
            result.Messages.Add(new("No one in the party is able to move!", MessageKind.Bad));
            return result;
        }
        var dir = kind switch
        {
            MoveKind.Back => State.Facing.Opposite(),
            MoveKind.StrafeLeft => State.Facing.Left(),
            MoveKind.StrafeRight => State.Facing.Right(),
            _ => State.Facing,
        };
        var map = CurrentMap;
        var (wall, solid) = map.Probe(State.X, State.Y, dir);
        if (solid || wall == WallKind.Wall || (wall == WallKind.SecretDoor && !State.IsSecretFound(map.Id, State.X, State.Y, dir)))
        {
            result.Messages.Add(new(map.Def.Kind == MapKind.Dungeon ? "Ouch! A solid wall." : "The way is blocked.", MessageKind.Info, "bump"));
            return result;
        }
        if (wall == WallKind.LockedDoor && !CanOpenLocks(map))
        {
            result.Messages.Add(new("The door is locked tight.", MessageKind.Info, "bump"));
            return result;
        }
        var nx = State.X + dir.Dx();
        var ny = State.Y + dir.Dy();
        foreach (var ev in map.EventsAt(nx, ny))
        {
            if (ev.Blocking && !IsCompleted(map, ev) && !RequirementsMet(ev))
            {
                result.Messages.Add(new(ev.FailText ?? "Something bars the way.", MessageKind.Story, "bump"));
                return result;
            }
        }

        var wasDark = map.IsDarkness(State.X, State.Y);
        var wasAntiMagic = map.IsAntiMagic(State.X, State.Y);
        State.X = nx;
        State.Y = ny;
        result.Moved = true;
        if (map.IsDarkness(nx, ny) && !wasDark)
        {
            result.Messages.Add(new("An unnatural darkness swallows every light.", MessageKind.Bad));
        }
        if (map.IsAntiMagic(nx, ny) && !wasAntiMagic)
        {
            result.Messages.Add(new("The air turns dead and still. Your magic falls silent.", MessageKind.Bad));
        }
        if (wall is WallKind.Door or WallKind.LockedDoor or WallKind.SecretDoor)
        {
            result.Messages.Add(new(wall == WallKind.LockedDoor ? "The lock clicks open." : "", MessageKind.Info, "door"));
        }
        else
        {
            result.Messages.Add(new("", MessageKind.Info, "step"));
        }
        result.Messages.RemoveAll(m => m.Text.Length == 0 && m.Sound is null);
        PassTime(1, result.Messages);
        Explore();
        TriggerEvents(result, entering: true);
        if (!result.CombatStarted && result.Interaction is null && !result.MapChanged && !result.Victory)
        {
            TryRandomEncounter(result, CurrentMap.Def.EncounterChance);
        }
        return result;
    }

    /// <summary>Re-uses the current cell (re-enter a shop, read a sign again).</summary>
    public StepResult Interact()
    {
        var result = new StepResult();
        if (Combat is not null || !IsActive)
        {
            return result;
        }
        TriggerEvents(result, entering: false);
        if (result.Messages.Count == 0 && result.Interaction is null && !result.CombatStarted && result.StoryText is null)
        {
            result.Messages.Add(new("There is nothing of interest here.", MessageKind.Info));
        }
        return result;
    }

    /// <summary>Rests for eight hours: eats food, restores HP and SP. May be interrupted by monsters.</summary>
    public StepResult Rest()
    {
        var result = new StepResult();
        if (Combat is not null || !IsActive)
        {
            return result;
        }
        var map = CurrentMap;
        if (map.Def.EncounterChance > 0 && Random.Chance(Math.Min(40, map.Def.EncounterChance * 4)))
        {
            result.Messages.Add(new("The party's rest is interrupted!", MessageKind.Bad, "roar"));
            TryRandomEncounter(result, 100);
            if (result.CombatStarted)
            {
                return result;
            }
        }
        PassTime(50, result.Messages);
        foreach (var c in State.Party.Where(c => c.IsAlive))
        {
            RestCharacter(c, result.Messages, requireFood: true);
        }
        result.Messages.Insert(0, new("The party rests for eight hours.", MessageKind.Info));
        return result;
    }

    /// <summary>Rest logic for one character (used by camping and the inn).</summary>
    /// <param name="c">Character.</param>
    /// <param name="log">Messages.</param>
    /// <param name="requireFood">Whether a food unit is consumed.</param>
    public void RestCharacter(Character c, List<GameMessage> log, bool requireFood)
    {
        c.Conditions &= ~Condition.Asleep;
        if (requireFood)
        {
            if (c.Food <= 0)
            {
                log.Add(new($"{c.Name} has no food and cannot recover.", MessageKind.Bad));
                return;
            }
            c.Food--;
        }
        c.Sp = c.MaxSp;
        if (c.Has(Condition.Poisoned))
        {
            log.Add(new($"{c.Name} is too sick with poison to heal.", MessageKind.Bad));
            return;
        }
        var amount = c.Has(Condition.Diseased) ? (c.MaxHp - Math.Max(c.Hp, 0)) / 2 : c.MaxHp;
        if (c.Has(Condition.Unconscious) && c.Hp <= 0)
        {
            c.Hp = 0;
        }
        Rulebook.Heal(c, Math.Max(1, amount));
    }

    /// <summary>Uses an item or casts a spell while exploring (handles Recall teleport).</summary>
    /// <param name="r">Result from <see cref="SpellCaster"/>.</param>
    public StepResult AfterExploreMagic(SpellResult r)
    {
        var result = new StepResult();
        result.Messages.AddRange(r.Messages);
        if (r.Teleported)
        {
            result.MapChanged = true;
            Explore();
        }
        return result;
    }

    /// <summary>
    /// Whether the party could step from a cell in a direction right now, ignoring map events:
    /// walls, solid terrain, locked doors (needs the map's key or flag) and undiscovered secret doors block.
    /// </summary>
    /// <param name="x">Cell X.</param>
    /// <param name="y">Cell Y.</param>
    /// <param name="dir">Direction of travel.</param>
    public bool CanPass(int x, int y, Direction dir)
    {
        var map = CurrentMap;
        var (wall, solid) = map.Probe(x, y, dir);
        return !solid && wall switch
        {
            WallKind.Wall => false,
            WallKind.LockedDoor => CanOpenLocks(map),
            WallKind.SecretDoor => State.IsSecretFound(map.Id, x, y, dir),
            _ => true,
        };
    }

    /// <summary>Chance (percent) that a party member spots a hidden door when searching.</summary>
    /// <param name="c">Searcher.</param>
    public int SearchChance(Character c) =>
        Math.Clamp(35 + 10 * (Rules.Bonus(c, Stat.Intellect) + Rules.Bonus(c, Stat.Luck)) + Rules.Thievery(c) / 2, 20, 95);

    /// <summary>
    /// Searches the walls around the party for secret doors. Takes a few minutes of game time and,
    /// like a step, may attract wandering monsters. The best searcher in the party rolls once per hidden door.
    /// </summary>
    public StepResult Search()
    {
        var result = new StepResult();
        if (Combat is not null || !IsActive)
        {
            return result;
        }
        var searcher = State.Party.Where(c => c.CanAct).OrderByDescending(SearchChance).FirstOrDefault();
        if (searcher is null)
        {
            result.Messages.Add(new("No one in the party is able to search!", MessageKind.Bad));
            return result;
        }
        var map = CurrentMap;
        result.Messages.Add(new($"{searcher.Name} searches the walls carefully...", MessageKind.Info, "step"));
        PassTime(5, result.Messages);
        var found = 0;
        foreach (var d in Enum.GetValues<Direction>())
        {
            if (map.GetWall(State.X, State.Y, d) == WallKind.SecretDoor &&
                !State.IsSecretFound(map.Id, State.X, State.Y, d) &&
                Random.Chance(SearchChance(searcher)))
            {
                State.FoundSecrets.Add(GameState.SecretKey(map.Id, State.X, State.Y, d));
                result.Messages.Add(new($"{searcher.Name} discovers a secret door to the {DescribeSide(d)}!", MessageKind.Good, "door"));
                found++;
            }
        }
        if (found == 0)
        {
            result.Messages.Add(new("You find nothing unusual.", MessageKind.Info));
            TryRandomEncounter(result, map.Def.EncounterChance);
        }
        return result;
    }

    private string DescribeSide(Direction d) =>
        d == State.Facing ? "front" : d == State.Facing.Opposite() ? "rear" : d == State.Facing.Left() ? "left" : "right";

    private bool CanOpenLocks(GameMap map) =>
        (map.Def.LockedDoorKey is { } key && Inventory_AnyoneHas(key)) ||
        (map.Def.LockedDoorFlag is { } flag && State.Flags.Contains(flag));

    private bool Inventory_AnyoneHas(string itemId) => Items.Inventory.AnyoneHas(State.Party, itemId);

    private void PassTime(int steps, List<GameMessage> log)
    {
        var before = State.Steps;
        State.Steps += steps;
        State.LightSteps = Math.Max(0, State.LightSteps - steps);
        var ticks = (int)(State.Steps / 10 - before / 10);
        if (ticks <= 0)
        {
            return;
        }
        foreach (var c in State.Party.Where(c => c.IsAlive && c.Has(Condition.Poisoned)))
        {
            if (Rules.ApplyDamage(c, ticks))
            {
                log.Add(new(c.Has(Condition.Dead) ? $"{c.Name} dies of poison!" : $"{c.Name} collapses from poison!", MessageKind.Bad));
            }
        }
    }

    /// <summary>Marks the cells around the party as explored on the automap.</summary>
    public void Explore()
    {
        var map = CurrentMap;
        void Mark(int x, int y) => State.MarkExplored(map.Id, map.Width, map.Height, x, y);
        Mark(State.X, State.Y);
        foreach (var d in Enum.GetValues<Direction>())
        {
            var (wall, _) = map.Probe(State.X, State.Y, d);
            var hidden = wall == WallKind.SecretDoor && !State.IsSecretFound(map.Id, State.X, State.Y, d);
            if (map.Def.Kind != MapKind.Dungeon || (wall != WallKind.Wall && !hidden))
            {
                Mark(State.X + d.Dx(), State.Y + d.Dy());
            }
        }
        if (map.Def.Kind != MapKind.Dungeon)
        {
            for (var dy = -1; dy <= 1; dy += 2)
            {
                for (var dx = -1; dx <= 1; dx += 2)
                {
                    Mark(State.X + dx, State.Y + dy);
                }
            }
        }
    }

    private static string EventKey(GameMap map, MapEventDef ev) =>
        ev.Id ?? $"{map.Id}:{ev.X}:{ev.Y}:{map.Def.Events.IndexOf(ev)}";

    private bool IsCompleted(GameMap map, MapEventDef ev) => ev.Once && State.CompletedEvents.Contains(EventKey(map, ev));

    /// <summary>Whether an event's flag and item requirements are satisfied.</summary>
    /// <param name="ev">Event.</param>
    public bool RequirementsMet(MapEventDef ev) =>
        (ev.RequiresFlag is null || State.Flags.Contains(ev.RequiresFlag)) &&
        (ev.RequiresNotFlag is null || !State.Flags.Contains(ev.RequiresNotFlag)) &&
        (ev.RequiresItem is null || Inventory_AnyoneHas(ev.RequiresItem));

    private void TriggerEvents(StepResult result, bool entering)
    {
        var map = CurrentMap;
        foreach (var ev in map.EventsAt(State.X, State.Y).ToList())
        {
            if (IsCompleted(map, ev))
            {
                continue;
            }
            if (!RequirementsMet(ev))
            {
                if (ev.FailText is not null)
                {
                    result.Messages.Add(new(ev.FailText, MessageKind.Story));
                }
                continue;
            }
            if (!entering && ev.Type is MapEventKind.Trap or MapEventKind.Encounter or MapEventKind.Teleport)
            {
                continue;
            }
            RunEvent(map, ev, result);
            // A quest step ends processing for this visit so follow-up texts don't stack up.
            if (result.MapChanged || result.CombatStarted || result.Victory || ev.Type == MapEventKind.Quest)
            {
                return;
            }
        }
    }

    private void Complete(GameMap map, MapEventDef ev)
    {
        if (ev.Once)
        {
            State.CompletedEvents.Add(EventKey(map, ev));
        }
        if (ev.SetFlag is not null)
        {
            State.Flags.Add(ev.SetFlag);
        }
    }

    private void Story(StepResult result, MapEventDef ev)
    {
        if (!string.IsNullOrWhiteSpace(ev.Text))
        {
            result.StoryTitle = ev.Name;
            result.StoryText = result.StoryText is null ? ev.Text : result.StoryText + "\n\n" + ev.Text;
            result.Messages.Add(new(ev.Text, MessageKind.Story));
        }
    }

    private void RunEvent(GameMap map, MapEventDef ev, StepResult result)
    {
        switch (ev.Type)
        {
            case MapEventKind.Message:
                Story(result, ev);
                Complete(map, ev);
                break;
            case MapEventKind.Shop:
            case MapEventKind.Temple:
            case MapEventKind.Tavern:
            case MapEventKind.Training:
                result.Interaction = ev;
                break;
            case MapEventKind.Inn:
                State.RecallMap = map.Id;
                State.RecallX = ev.X;
                State.RecallY = ev.Y;
                result.Interaction = ev;
                break;
            case MapEventKind.Teleport:
                Story(result, ev);
                Complete(map, ev);
                var destMap = ev.Map ?? map.Id;
                result.MapChanged = destMap != map.Id;
                State.MapId = destMap;
                State.X = ev.ToX;
                State.Y = ev.ToY;
                if (ev.Facing is { } f)
                {
                    State.Facing = f;
                }
                result.Moved = true;
                result.Messages.Add(new("", MessageKind.Info, "door"));
                Explore();
                break;
            case MapEventKind.Treasure:
            case MapEventKind.Quest:
                if (ev.ConsumeItem && ev.RequiresItem is not null)
                {
                    Items.Inventory.RemoveFirst(State.Party, ev.RequiresItem);
                }
                Story(result, ev);
                GrantRewards(ev, result.Messages);
                Complete(map, ev);
                break;
            case MapEventKind.Encounter:
                {
                    Story(result, ev);
                    var monsters = ev.Monsters.SelectMany(fm => CombatEngine.Spawn(Content.Monster(fm.Monster), fm.Count.Roll(Random), Random)).ToList();
                    if (monsters.Count > 0)
                    {
                        StartCombat(monsters, result);
                        _combatEventKey = ev.Once ? EventKey(map, ev) : null;
                        if (ev.SetFlag is not null)
                        {
                            _combatFlag = ev.SetFlag;
                        }
                    }
                    break;
                }
            case MapEventKind.Trap:
                Complete(map, ev);
                RunTrap(map, ev, result);
                break;
            case MapEventKind.Spinner:
                // Classic spinner: no message, the party simply ends up facing a random way.
                State.Facing = (Direction)Random.Next(0, 4);
                if (!string.IsNullOrWhiteSpace(ev.Text))
                {
                    result.Messages.Add(new(ev.Text, MessageKind.Info));
                }
                Complete(map, ev);
                break;
            case MapEventKind.Fountain:
                Story(result, ev);
                foreach (var c in State.Party.Where(c => c.IsAlive))
                {
                    if (ev.Heal)
                    {
                        Rulebook.Heal(c, c.MaxHp);
                    }
                    if (ev.RestoreSp)
                    {
                        c.Sp = c.MaxSp;
                    }
                    c.Conditions &= ~ev.Conditions;
                }
                result.Messages.Add(new("The party feels refreshed.", MessageKind.Good, "heal"));
                Complete(map, ev);
                break;
            case MapEventKind.Victory:
                Story(result, ev);
                GrantRewards(ev, result.Messages);
                Complete(map, ev);
                State.Won = true;
                result.Victory = true;
                break;
        }
    }

    private string? _combatFlag;

    private void RunTrap(GameMap map, MapEventDef ev, StepResult result)
    {
        var best = State.Party.Where(c => c.CanAct).OrderByDescending(Rules.Thievery).FirstOrDefault();
        var skill = best is null ? 0 : Rules.Thievery(best);
        if (best is not null && skill > 0 && Random.Chance(skill))
        {
            result.Messages.Add(new($"{best.Name} spots and disarms a trap!", MessageKind.Good, "chest"));
            return;
        }
        result.Messages.Add(new(ev.Text ?? "A trap is sprung!", MessageKind.Bad, "party_hurt"));
        switch (ev.Trap)
        {
            case TrapEffect.Alarm:
                {
                    var monsters = ev.Monsters.SelectMany(fm => CombatEngine.Spawn(Content.Monster(fm.Monster), fm.Count.Roll(Random), Random)).ToList();
                    if (monsters.Count > 0)
                    {
                        StartCombat(monsters, result);
                        _combatEventKey = null;
                    }
                    else
                    {
                        TryRandomEncounter(result, 100);
                    }
                    return;
                }
            case TrapEffect.Teleport:
                TrapTeleport(map, ev, result);
                return;
            case TrapEffect.Pit:
                DamageParty(ev, result);
                TrapTeleport(map, ev, result);
                return;
            default:
                DamageParty(ev, result);
                SleepItOff(result);
                return;
        }
    }

    /// <summary>
    /// Outside combat, sleep wears off: the party loses some time (and may be ambushed while helpless),
    /// then wakes up. Without this a party put entirely to sleep could never act again.
    /// </summary>
    private void SleepItOff(StepResult result)
    {
        if (!State.Party.Any(c => c.IsAlive && c.Has(Condition.Asleep)))
        {
            return;
        }
        PassTime(30, result.Messages);
        TryRandomEncounter(result, Math.Min(60, CurrentMap.Def.EncounterChance * 5));
        if (result.CombatStarted)
        {
            result.Messages.Add(new("Monsters fall upon the sleeping party!", MessageKind.Bad));
            return; // sleepers wake when hit, or when the battle ends
        }
        foreach (var c in State.Party)
        {
            c.Conditions &= ~Condition.Asleep;
        }
        result.Messages.Add(new("Some time later, the party wakes up groggy but unharmed.", MessageKind.Info));
    }

    private void TrapTeleport(GameMap map, MapEventDef ev, StepResult result)
    {
        if (ev.Map is not null || ev.ToX != 0 || ev.ToY != 0)
        {
            var dest = ev.Map ?? map.Id;
            result.MapChanged = dest != map.Id;
            State.MapId = dest;
            State.X = ev.ToX;
            State.Y = ev.ToY;
        }
        else
        {
            // Somewhere random the party could have walked to, avoiding squares with events.
            var options = ReachableNow().Where(p => map.EventsAt(p.X, p.Y).Count == 0 && p != (State.X, State.Y)).ToList();
            if (options.Count > 0)
            {
                (State.X, State.Y) = Random.Pick(options);
            }
        }
        result.Moved = true;
        result.Messages.Add(new("The world lurches - you are somewhere else!", MessageKind.Bad, "spell"));
        Explore();
    }

    /// <summary>Cells the party can currently walk to from where it stands (respects locks and undiscovered secret doors).</summary>
    public IReadOnlyList<(int X, int Y)> ReachableNow()
    {
        var seen = new HashSet<(int, int)> { (State.X, State.Y) };
        var queue = new Queue<(int X, int Y)>();
        queue.Enqueue((State.X, State.Y));
        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            foreach (var d in Enum.GetValues<Direction>())
            {
                var n = (x + d.Dx(), y + d.Dy());
                if (CanPass(x, y, d) && seen.Add(n))
                {
                    queue.Enqueue(n);
                }
            }
        }
        return seen.ToList();
    }

    private void DamageParty(MapEventDef ev, StepResult result)
    {
        foreach (var c in State.Party.Where(c => c.IsAlive))
        {
            var dmg = ev.Damage.Roll(Random);
            if (Rules.SavingThrow(c, 3, Random))
            {
                dmg /= 2;
            }
            else if (ev.Conditions != Condition.None)
            {
                c.Conditions |= ev.Conditions;
            }
            if (dmg > 0)
            {
                result.Messages.Add(new($"{c.Name} takes {dmg} damage.", MessageKind.Bad));
                if (Rules.ApplyDamage(c, dmg))
                {
                    result.Messages.Add(new(c.Has(Condition.Dead) ? $"{c.Name} is killed!" : $"{c.Name} falls unconscious!", MessageKind.Bad));
                }
            }
        }
    }

    private void GrantRewards(MapEventDef ev, List<GameMessage> log)
    {
        var gold = ev.Gold.Roll(Random);
        if (gold > 0)
        {
            State.Gold += gold;
            log.Add(new($"The party finds {gold} gold.", MessageKind.Loot, "coins"));
        }
        if (ev.Gems > 0)
        {
            State.Gems += ev.Gems;
            log.Add(new($"The party finds {ev.Gems} gem{(ev.Gems > 1 ? "s" : "")}.", MessageKind.Loot, "coins"));
        }
        foreach (var itemId in ev.Items)
        {
            GiveItem(new ItemInstance(itemId, Content.Item(itemId).Charges), log);
        }
        if (ev.Xp > 0)
        {
            AwardXp(ev.Xp, log);
        }
    }

    /// <summary>Gives an item to the first party member with room.</summary>
    /// <param name="item">Item.</param>
    /// <param name="log">Messages.</param>
    public bool GiveItem(ItemInstance item, List<GameMessage> log)
    {
        var taker = State.Party.FirstOrDefault(c => c.IsAlive && !c.BackpackFull) ?? State.Party.FirstOrDefault(c => !c.BackpackFull);
        var name = Content.Item(item.ItemId).Name;
        if (taker is null)
        {
            log.Add(new($"No one has room for {name}; it is left behind.", MessageKind.Bad));
            return false;
        }
        taker.Backpack.Add(item);
        log.Add(new($"{taker.Name} takes {name}.", MessageKind.Loot, "chest"));
        return true;
    }

    private void AwardXp(int xp, List<GameMessage> log)
    {
        var eligible = State.Party.Where(c => c.IsAlive && !c.Has(Condition.Unconscious)).ToList();
        foreach (var c in eligible)
        {
            var could = Rules.CanLevelUp(c);
            c.Experience += xp;
            if (!could && Rules.CanLevelUp(c))
            {
                log.Add(new($"{c.Name} is ready to train for level {c.Level + 1}!", MessageKind.Good, "levelup"));
            }
        }
        if (eligible.Count > 0)
        {
            log.Add(new($"Each conscious member gains {xp} experience.", MessageKind.Loot));
        }
    }

    private void TryRandomEncounter(StepResult result, int chance)
    {
        var map = CurrentMap;
        if (map.Def.Encounters.Count == 0 || !Random.Chance(chance))
        {
            return;
        }
        var monsters = new List<MonsterInstance>();
        var groups = Random.Chance(30) ? 2 : 1;
        for (var g = 0; g < groups; g++)
        {
            var entry = PickWeighted(map.Def.Encounters);
            monsters.AddRange(CombatEngine.Spawn(Content.Monster(entry.Monster), entry.Count.Roll(Random), Random));
        }
        var ordered = monsters.Take(8).ToList();
        StartCombat(ordered, result);
        _combatEventKey = null;
    }

    private EncounterEntryDef PickWeighted(List<EncounterEntryDef> entries)
    {
        var total = entries.Sum(e => Math.Max(1, e.Weight));
        var roll = Random.Next(0, total);
        foreach (var e in entries)
        {
            roll -= Math.Max(1, e.Weight);
            if (roll < 0)
            {
                return e;
            }
        }
        return entries[^1];
    }

    /// <summary>Begins a battle with specific monsters.</summary>
    /// <param name="monsters">Opponents.</param>
    /// <param name="result">Result to annotate.</param>
    public void StartCombat(IEnumerable<MonsterInstance> monsters, StepResult result)
    {
        Combat = new CombatEngine(Rules, Random, State, monsters) { MagicSuppressed = () => IsAntiMagicHere };
        _combatFlag = null;
        result.CombatStarted = true;
        var names = Combat.Monsters.GroupBy(m => m.Def).Select(g => g.Count() == 1 ? $"a {g.Key.Name}" : $"{g.Count()} {g.Key.PluralName}");
        result.Messages.Add(new($"Encounter! The party faces {string.Join(", ", names)}.", MessageKind.Bad, "roar"));
    }

    /// <summary>Closes the current battle, distributing rewards on victory.</summary>
    /// <returns>Narration and the final outcome.</returns>
    public (CombatOutcome Outcome, List<GameMessage> Messages) EndCombat()
    {
        var log = new List<GameMessage>();
        var combat = Combat;
        if (combat is null)
        {
            return (CombatOutcome.Ongoing, log);
        }
        if (combat.Outcome == CombatOutcome.Victory && combat.Rewards is { } r)
        {
            if (r.Gold > 0)
            {
                State.Gold += r.Gold;
                log.Add(new($"The party collects {r.Gold} gold.", MessageKind.Loot, "coins"));
            }
            if (r.Gems > 0)
            {
                State.Gems += r.Gems;
                log.Add(new($"The party finds {r.Gems} gem{(r.Gems > 1 ? "s" : "")}.", MessageKind.Loot));
            }
            foreach (var item in r.Items)
            {
                GiveItem(item, log);
            }
            if (r.Experience > 0)
            {
                AwardXp(r.Experience, log);
            }
            if (_combatEventKey is not null)
            {
                State.CompletedEvents.Add(_combatEventKey);
            }
            if (_combatFlag is not null)
            {
                State.Flags.Add(_combatFlag);
            }
        }
        combat.Finish();
        _combatEventKey = null;
        _combatFlag = null;
        Combat = null;
        return (combat.Outcome, log);
    }
}
