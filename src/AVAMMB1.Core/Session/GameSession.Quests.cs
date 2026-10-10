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
        if (RequirementsMet(best.Ev))
        {
            return best;
        }
        return depth < 3 && best.Ev.RequiresFlag is { } flag && !State.Flags.Contains(flag)
            ? FindGoal(ev => ev.SetFlag == flag, depth + 1)
            : null;
    }
}
