using AVAMMB1.Core.Characters;
using AVAMMB1.Core.Combat;
using AVAMMB1.Core.Content;
using AVAMMB1.Core.Dice;
using AVAMMB1.Core.Items;
using AVAMMB1.Core.Magic;
using AVAMMB1.Core.Rules;
using AVAMMB1.Core.World;

namespace AVAMMB1.Core.Session;

/// <summary>"Where next?" for open quests.</summary>
public sealed partial class GameSession
{
    /// <summary>
    /// Where each open quest leads next: the place that moves it to its next stage (sets the next flag, gives
    /// the next item, leads to the next map) - or, failing that, the place that wants what the party has just
    /// gained. A stage's explicit <see cref="QuestStageDef.Goal"/> wins.
    /// </summary>
    public IReadOnlyList<QuestGoal> QuestGoals()
    {
        var goals = new List<QuestGoal>();
        foreach (var q in Content.Quests)
        {
            if (q.DoneFlag is not null && State.Flags.Contains(q.DoneFlag))
            {
                continue;
            }
            var current = q.Stages.FindLastIndex(st => QuestJournal.Reached(st, State));
            if (current < 0)
            {
                continue;
            }
            var stage = q.Stages[current];
            if (stage.Goal is { } g && Content.Maps.TryGetValue(g.Map, out var gm))
            {
                goals.Add(new QuestGoal(q.Id, q.Title, gm.Id, gm.Def.Name, g.X, g.Y, g.Name ?? gm.Def.Name));
                continue;
            }
            var next = current + 1 < q.Stages.Count ? q.Stages[current + 1] : null;
            var found = FindGoal(ev => next is not null ? Advances(ev, next) : q.DoneFlag is not null && ev.SetFlag == q.DoneFlag)
                ?? FindGoal(ev => (stage.Item is not null && ev.RequiresItem == stage.Item) || (stage.Flag is not null && ev.RequiresFlag == stage.Flag));
            if (found is { } f)
            {
                goals.Add(new QuestGoal(q.Id, q.Title, f.Map.Id, f.Map.Def.Name, f.Ev.X, f.Ev.Y, f.Ev.Name ?? f.Map.Def.Name));
            }
        }
        return goals;
    }

    /// <summary>Whether an event moves a quest to the given stage.</summary>
    private bool Advances(MapEventDef ev, QuestStageDef stage) =>
        (stage.Flag is not null && (ev.SetFlag == stage.Flag || ev.Options.Any(o => o.SetFlags.Contains(stage.Flag))))
        || (stage.Item is not null && (ev.Items.Contains(stage.Item)
            || ev.Monsters.Any(m => Content.Monsters.TryGetValue(m.Monster, out var md) && md.Drops.Any(d => d.Item == stage.Item && d.Chance >= 100))))
        || (stage.Visited is not null && ev.Type == MapEventKind.Teleport && ev.Map == stage.Visited);

    /// <summary>
    /// The first open event matching a test - available ones first, quest-givers first, then the party's own
    /// and explored maps. If the only match is still locked behind a flag (a hoard behind its guardian), the
    /// event that sets that flag is the goal instead.
    /// </summary>
    private (GameMap Map, MapEventDef Ev)? FindGoal(Func<MapEventDef, bool> match, int depth = 0)
    {
        var hits = Content.Maps.Values
            .SelectMany(m => m.AllEvents.Select(ev => (Map: m, Ev: ev)))
            .Where(x => match(x.Ev) && !IsCompleted(x.Map, x.Ev))
            .OrderByDescending(x => RequirementsMet(x.Ev))
            .ThenByDescending(x => CanReach(x.Map, x.Ev.X, x.Ev.Y))
            .ThenByDescending(x => x.Ev.Type == MapEventKind.Quest)
            .ThenByDescending(x => x.Map.Id == State.MapId)
            .ThenByDescending(x => State.Explored.ContainsKey(x.Map.Id))
            .ThenBy(x => x.Map.Id, StringComparer.Ordinal)
            .ToList();
        if (hits.Count == 0)
        {
            return null;
        }
        var best = hits[0];
        if (RequirementsMet(best.Ev) && !CanReach(best.Map, best.Ev.X, best.Ev.Y) && depth < 3)
        {
            // Behind a locked door: the key (or whatever opens the map's locks) comes first.
            var def = best.Map.Def;
            if (def.LockedDoorKey is { } key && !Inventory_AnyoneHas(key)
                && FindGoal(ev => ev.Items.Contains(key) || ev.Monsters.Any(m => Content.Monsters.TryGetValue(m.Monster, out var md) && md.Drops.Any(d => d.Item == key)), depth + 1) is { } keyGoal)
            {
                return keyGoal;
            }
            if (def.LockedDoorFlag is { } doorFlag && !State.Flags.Contains(doorFlag) && FindGoal(ev => ev.SetFlag == doorFlag, depth + 1) is { } flagGoal)
            {
                return flagGoal;
            }
        }
        if (RequirementsMet(best.Ev))
        {
            return best;
        }
        return depth < 3 && best.Ev.RequiresFlag is { } flag && !State.Flags.Contains(flag)
            ? FindGoal(ev => ev.SetFlag == flag, depth + 1)
            : null;
    }

    /// <summary>
    /// Whether a square can be reached on a map from where its passages arrive - through doors the party
    /// can open (its key, a pick-lock, a lock already picked) and secret doors (which can be searched for),
    /// but not past squares barred by unmet requirements.
    /// </summary>
    private bool CanReach(GameMap map, int tx, int ty)
    {
        var starts = Content.Maps.Values.SelectMany(m => m.AllEvents)
            .Where(e => e.Type == MapEventKind.Teleport && e.Map == map.Id).Select(e => (e.ToX, e.ToY))
            .Concat(map.AllEvents.Where(e => e.Type == MapEventKind.Teleport).Select(e => (e.X, e.Y)))
            .Where(c => map.InBounds(c.Item1, c.Item2))
            .ToHashSet();
        if (map.Id == State.MapId)
        {
            starts.Add((State.X, State.Y));
        }
        var picker = !map.Def.MasterLocks && State.Party.Any(c => c.IsAlive && Rules.HasAbility(c, ClassAbility.PickLocks));
        var seen = new HashSet<(int, int)>(starts);
        var q = new Queue<(int X, int Y)>(starts);
        while (q.Count > 0)
        {
            var (x, y) = q.Dequeue();
            if ((x, y) == (tx, ty))
            {
                return true;
            }
            foreach (var d in Enum.GetValues<Direction>())
            {
                var (wall, solid) = map.Probe(x, y, d);
                var n = (X: x + d.Dx(), Y: y + d.Dy());
                var open = !solid && wall switch
                {
                    WallKind.Wall => false,
                    WallKind.LockedDoor => picker || CanOpenLocks(map) || State.PickedLocks.Contains(GameState.SecretKey(map.Id, x, y, d)),
                    _ => true,
                };
                if (!open || !map.InBounds(n.X, n.Y) || !seen.Add(n))
                {
                    continue;
                }
                if (n != (tx, ty) && map.EventsAt(n.X, n.Y).Any(e => e.Blocking && !IsCompleted(map, e) && !RequirementsMet(e)))
                {
                    continue;
                }
                q.Enqueue(n);
            }
        }
        return false;
    }
}
