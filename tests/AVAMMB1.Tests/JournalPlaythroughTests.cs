using AVAMMB1.Core.Content;
using AVAMMB1.Core.Items;
using AVAMMB1.Core.Rules;
using AVAMMB1.Core.Session;
using Xunit.Abstractions;

namespace AVAMMB1.Tests;

/// <summary>
/// Plays the whole game the way a player following the journal would: take each open quest's "Where
/// next?" goal, travel there through the map connections with real movement, and step onto it (or
/// talk, answer, choose). Fights are won outright - this guards that every quest can be finished and
/// that the goals lead somewhere, not how hard the way is.
/// </summary>
public class JournalPlaythroughTests(ITestOutputHelper output)
{
    private static GameSession Party()
    {
        var s = TestContent.StartedSession(seed: 21);
        foreach (var c in s.State.Party)
        {
            c.Experience = Rulebook.XpForLevel(s.Content.Class(c.Class), 16);
            while (s.Rules.LevelUp(c, s.Random) is not null) { }
        }
        s.State.Gold = 1_000_000;
        s.State.Party[0].Equipment[EquipSlot.Light] = new ItemInstance("lantern_everburning");
        return s;
    }

    /// <summary>The teleports that lead from map to map (open now, fares affordable).</summary>
    private static List<MapEventDef>? Route(GameSession s, string to)
    {
        var from = s.State.MapId;
        if (from == to)
        {
            return [];
        }
        var prev = new Dictionary<string, (string Map, MapEventDef Ev)>(StringComparer.Ordinal) { [from] = (from, null!) };
        var q = new Queue<string>([from]);
        while (q.Count > 0)
        {
            var map = q.Dequeue();
            foreach (var ev in s.Content.Map(map).AllEvents.Where(e => e.Type == MapEventKind.Teleport && e.Map is not null && e.Map != map && s.RequirementsMet(e)))
            {
                if (prev.ContainsKey(ev.Map!) || ev.Map == AVAMMB1.Core.World.Depths.MapId)
                {
                    continue;
                }
                prev[ev.Map!] = (map, ev);
                if (ev.Map == to)
                {
                    var hops = new List<MapEventDef>();
                    for (var m = to; m != from; m = prev[m].Map)
                    {
                        hops.Add(prev[m].Ev);
                    }
                    hops.Reverse();
                    return hops;
                }
                q.Enqueue(ev.Map!);
            }
        }
        return null;
    }

    /// <summary>Where a quest the party has not heard of yet begins (a player finds these by exploring).</summary>
    private static QuestGoal? Starter(GameSession s)
    {
        foreach (var q in s.Content.Quests)
        {
            var first = q.Stages[0];
            if (QuestJournal.Reached(first, s.State) || (q.DoneFlag is not null && s.State.Flags.Contains(q.DoneFlag)))
            {
                continue;
            }
            foreach (var map in s.Content.Maps.Values.Where(m => m.Id != AVAMMB1.Core.World.Depths.MapId))
            {
                foreach (var ev in map.AllEvents.Where(s.RequirementsMet))
                {
                    var starts = (first.Flag is not null && (ev.SetFlag == first.Flag || ev.Options.Any(o => o.SetFlags.Contains(first.Flag))))
                        || (first.Item is not null && ev.Items.Contains(first.Item))
                        || (first.Visited is not null && ev.Type == MapEventKind.Teleport && ev.Map == first.Visited);
                    if (starts)
                    {
                        return new QuestGoal(q.Id, q.Title, map.Id, map.Def.Name, ev.X, ev.Y, ev.Name ?? map.Def.Name);
                    }
                }
            }
        }
        return null;
    }

    private static void Heal(GameSession s)
    {
        foreach (var c in s.State.Party)
        {
            c.Conditions = Condition.None;
            c.Hp = c.MaxHp;
            c.Sp = c.MaxSp;
        }
    }

    private static void Settle(GameSession s, Walker walker, StepResult r)
    {
        if (r.CombatStarted && s.Combat is not null)
        {
            walker.Fight();
        }
        switch (r.Interaction)
        {
            case { Type: MapEventKind.Riddle } riddle:
                s.AnswerRiddle(riddle, riddle.Answers[0]);
                break;
            case { Type: MapEventKind.Choice } choice:
                s.Choose(choice, choice.Options.Count - 1); // the last option: sending the tithe home, etc.
                break;
        }
    }

    [Fact]
    public void EveryQuest_CanBeFinishedByFollowingTheJournal()
    {
        var s = Party();
        var walker = new Walker(s) { Overwhelm = true };
        var steps = new List<string>();
        for (var i = 0; i < 200; i++)
        {
            var goal = s.QuestGoals().OrderByDescending(g => s.Content.Quests.First(q => q.Id == g.QuestId).Main ? 0 : 1).FirstOrDefault()
                ?? Starter(s);
            if (goal is null)
            {
                break;
            }
            steps.Add($"{goal.QuestId}: {goal.Place} ({goal.MapId} {goal.X},{goal.Y})");
            Heal(s);
            var route = Route(s, goal.MapId) ?? throw new InvalidOperationException($"No way to {goal.MapId} from {s.State.MapId} for {goal.QuestId}.\n" + string.Join("\n", steps));
            foreach (var hop in route)
            {
                var r = walker.Go(hop.X, hop.Y);
                Settle(s, walker, r);
                Assert.True(s.State.MapId == hop.Map, $"{goal.QuestId}: the passage at {hop.X},{hop.Y} did not lead to {hop.Map}.\n" + string.Join("\n", steps));
            }
            StepResult arrive;
            try
            {
                arrive = (s.State.X, s.State.Y) == (goal.X, goal.Y) ? s.Interact() : walker.Go(goal.X, goal.Y);
            }
            catch (InvalidOperationException ex)
            {
                throw new InvalidOperationException(ex.Message + "\n" + string.Join("\n", steps.TakeLast(8)) + $"\nflags: {string.Join(",", s.State.Flags.Order())}", ex);
            }
            Settle(s, walker, arrive);
            if (s.State.Won && s.Content.Quests.All(q => q.DoneFlag is null || s.State.Flags.Contains(q.DoneFlag)))
            {
                break;
            }
        }
        output.WriteLine(string.Join("\n", steps));
        var open = s.Content.Quests.Where(q => q.DoneFlag is not null && !s.State.Flags.Contains(q.DoneFlag)).Select(q => q.Id).ToList();
        Assert.True(open.Count == 0, "Quests left unfinished: " + string.Join(", ", open) + "\n" + string.Join("\n", steps.TakeLast(10)));
        Assert.True(s.State.Won, "The main quest was not completed.\n" + string.Join("\n", steps.TakeLast(10)));
    }
}
