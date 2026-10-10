using AVAMMB1.Core.Combat;
using AVAMMB1.Core.Content;
using AVAMMB1.Core.Items;
using AVAMMB1.Core.Rules;
using AVAMMB1.Core.Session;
using AVAMMB1.Core.World;

namespace AVAMMB1.Tests;

/// <summary>
/// Walks a session around with normal movement commands (turn + step), fighting any battle with
/// simple tactics and re-planning after spinners, teleports or blocked steps.
/// </summary>
internal sealed class Walker(GameSession s)
{
    public int Battles { get; private set; }

    /// <summary>Win every battle outright (for tests about where things are, not how hard they are).</summary>
    public bool Overwhelm { get; set; }

    public StepResult? Last { get; private set; }

    private List<Direction>? Path(int tx, int ty)
    {
        var map = s.CurrentMap;
        var avoid = map.AllEvents.Where(e => e.Type == MapEventKind.Teleport).Select(e => (e.X, e.Y)).ToHashSet();
        avoid.Remove((tx, ty));
        var prev = new Dictionary<(int, int), ((int, int), Direction)> { [(s.State.X, s.State.Y)] = ((s.State.X, s.State.Y), Direction.North) };
        var q = new Queue<(int, int)>();
        q.Enqueue((s.State.X, s.State.Y));
        while (q.Count > 0)
        {
            var cur = q.Dequeue();
            if (cur == (tx, ty))
            {
                var dirs = new List<Direction>();
                while (cur != (s.State.X, s.State.Y))
                {
                    var (from, d) = prev[cur];
                    dirs.Add(d);
                    cur = from;
                }
                dirs.Reverse();
                return dirs;
            }
            foreach (var d in Enum.GetValues<Direction>())
            {
                var n = (cur.Item1 + d.Dx(), cur.Item2 + d.Dy());
                if (!s.CanPass(cur.Item1, cur.Item2, d) || avoid.Contains(n) || prev.ContainsKey(n))
                {
                    continue;
                }
                prev[n] = (cur, d);
                q.Enqueue(n);
            }
        }
        return null;
    }

    /// <summary>Walks to a cell; returns the result of the final step.</summary>
    /// <summary>Walks onto the first event on the current map matching a predicate.</summary>
    public StepResult GoTo(Func<MapEventDef, bool> match)
    {
        var e = s.CurrentMap.AllEvents.First(match);
        return Go(e.X, e.Y);
    }

    /// <summary>Takes the teleport/stairs leading to another map.</summary>
    public StepResult Travel(string destinationMap) =>
        GoTo(e => e.Type == MapEventKind.Teleport && e.Map == destinationMap);

    public StepResult Go(int tx, int ty)
    {
        var mapId = s.State.MapId;
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var path = Path(tx, ty) ?? throw new InvalidOperationException($"No path to ({tx},{ty}) on {mapId} from ({s.State.X},{s.State.Y}).");
            if (path.Count == 0)
            {
                return Last ?? new StepResult();
            }
            foreach (var d in path)
            {
                while (s.State.Facing != d)
                {
                    s.TurnRight();
                }
                var expected = (s.State.X + d.Dx(), s.State.Y + d.Dy());
                Last = s.Move(MoveKind.Forward);
                if (Last.CombatStarted)
                {
                    Fight();
                }
                if (s.State.MapId != mapId || (s.State.X, s.State.Y) == (tx, ty))
                {
                    return Last;
                }
                if (!Last.Moved || Last.CombatStarted || (s.State.X, s.State.Y) != expected)
                {
                    break; // re-plan after a fight, a blocked step or a teleport trap
                }
            }
        }
        throw new InvalidOperationException($"Could not reach ({tx},{ty}) on {mapId}.");
    }

    public void Fight()
    {
        var combat = s.Combat!;
        if (Overwhelm)
        {
            foreach (var m in combat.Monsters)
            {
                m.Hp = 0;
            }
        }
        combat.Advance();
        var guard = 0;
        while (combat.Outcome == CombatOutcome.Ongoing && guard++ < 2000)
        {
            var c = combat.ActiveCharacter!;
            var action = combat.IsInFrontRank(c) ? new CombatAction(CombatActionKind.Attack)
                : s.Rules.HasMissileWeapon(c) ? new CombatAction(CombatActionKind.Shoot)
                : new CombatAction(CombatActionKind.Block);
            combat.Act(action);
        }
        Assert.Equal(CombatOutcome.Victory, combat.Outcome);
        s.EndCombat();
        Battles++;
        foreach (var c in s.State.Party)
        {
            c.Conditions = Condition.None;
            c.Hp = c.MaxHp;
        }
    }
}
