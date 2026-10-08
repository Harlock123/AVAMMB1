using AVAMMB1.Core.Combat;
using AVAMMB1.Core.Content;
using AVAMMB1.Core.Items;
using AVAMMB1.Core.Rules;
using AVAMMB1.Core.Session;
using AVAMMB1.Core.World;

namespace AVAMMB1.Tests;

/// <summary>
/// Plays the whole quest line through the public session API: walking with normal moves,
/// fighting every encounter with simple tactics, and checking each story milestone.
/// The party is boosted so the test checks the content logic, not the balance.
/// </summary>
public class PlaythroughTests
{
    private sealed class Walker(GameSession s)
    {
        public int Battles { get; private set; }

        public StepResult? Last { get; private set; }

        private List<Direction>? Path(int tx, int ty)
        {
            var map = s.CurrentMap;
            var avoid = map.AllEvents.Where(e => e.Type == MapEventKind.Teleport).Select(e => (e.X, e.Y)).ToHashSet();
            avoid.Remove((tx, ty));
            var keys = map.Def.LockedDoorKey is { } k && Inventory.AnyoneHas(s.State.Party, k);
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
                    var (wall, solid) = map.Probe(cur.Item1, cur.Item2, d);
                    var n = (cur.Item1 + d.Dx(), cur.Item2 + d.Dy());
                    if (solid || wall == WallKind.Wall || (wall == WallKind.LockedDoor && !keys) || avoid.Contains(n) || prev.ContainsKey(n))
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
                    Last = s.Move(MoveKind.Forward);
                    if (Last.CombatStarted)
                    {
                        Fight();
                    }
                    if (s.State.MapId != mapId || (s.State.X, s.State.Y) == (tx, ty))
                    {
                        return Last;
                    }
                    if (!Last.Moved || Last.CombatStarted)
                    {
                        break; // re-plan after a fight or a blocked step
                    }
                }
            }
            throw new InvalidOperationException($"Could not reach ({tx},{ty}) on {mapId}.");
        }

        public void Fight()
        {
            var combat = s.Combat!;
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

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void QuestLine_CanBeCompletedFromStartToVictory(int seed)
    {
        var s = TestContent.StartedSession(seed);
        foreach (var c in s.State.Party)
        {
            c.Level = 15;
            c.MaxHp = c.Hp = 400;
            c.Stats[Stat.Accuracy] = 25;
            c.Stats[Stat.Might] = 25;
        }
        var w = new Walker(s);

        // Brindlemoor: meet Archivist Pell.
        Assert.NotNull(w.Go(3, 13).StoryText);
        Assert.Contains("pell_met", s.State.Flags);

        // Cellars: defeat the chieftain and take the Ember Shard.
        w.Go(12, 13);
        Assert.Equal("cellars", s.State.MapId);
        w.Go(13, 3);
        Assert.Contains("chieftain_dead", s.State.Flags);
        w.Go(14, 1);
        Assert.True(Inventory.AnyoneHas(s.State.Party, "ember_shard"));

        // Back to Pell for the crypt key.
        w.Go(1, 14);
        Assert.Equal("brindlemoor", s.State.MapId);
        w.Go(3, 13);
        Assert.True(Inventory.AnyoneHas(s.State.Party, "crypt_key"));

        // Wilds -> Hollow Crypt: defeat the lich, take the Frost Shard.
        w.Go(7, 15);
        Assert.Equal("wilds", s.State.MapId);
        w.Go(16, 17);
        Assert.Equal("crypt", s.State.MapId);
        w.Go(13, 2);
        Assert.Contains("lich_dead", s.State.Flags);
        w.Go(14, 1);
        Assert.True(Inventory.AnyoneHas(s.State.Party, "frost_shard"));

        // Saltreach: Sage Orrin forges the Vault Sigil.
        w.Go(0, 15);
        Assert.Equal("wilds", s.State.MapId);
        w.Go(15, 4);
        Assert.Equal("saltreach", s.State.MapId);
        w.Go(12, 12);
        Assert.True(Inventory.AnyoneHas(s.State.Party, "vault_sigil"));

        // The Inner Vault: defeat the Warden and restore the Lodestone.
        w.Go(0, 8);
        Assert.Equal("wilds", s.State.MapId);
        w.Go(3, 3);
        Assert.Equal("vault", s.State.MapId);
        w.Go(5, 3);
        Assert.Contains("warden_dead", s.State.Flags);
        var end = w.Go(6, 1);
        Assert.True(end.Victory);
        Assert.True(s.State.Won);
        Assert.True(w.Battles >= 3);
    }

    [Fact]
    public void FirstDungeon_IsBeatableByAFreshQuickParty()
    {
        // Balance sanity check: a level-1 premade party, resting and training like a player would,
        // can clear the chieftain in the first dungeon on most seeds.
        var wins = 0;
        for (var seed = 0; seed < 10; seed++)
        {
            var s = TestContent.StartedSession(seed);
            var ev = new MapEventDef { Type = MapEventKind.Training };
            // Grant enough XP for level 3 and train with the starting gold only
            // (gold runs out, so most members end up at level 2, a few at level 3).
            foreach (var c in s.State.Party)
            {
                c.Experience = Rulebook.XpForLevel(s.Content.Class(c.Class), 3);
                s.Town.Train(c, ev);
                s.Town.Train(c, ev);
            }
            s.State.MapId = "cellars";
            s.State.X = 13;
            s.State.Y = 4;
            s.State.Facing = Direction.North;
            var r = s.Move(MoveKind.Forward);
            Assert.True(r.CombatStarted);
            var combat = s.Combat!;
            combat.Advance();
            var guard = 0;
            while (combat.Outcome == CombatOutcome.Ongoing && guard++ < 2000)
            {
                var c = combat.ActiveCharacter!;
                var cleric = c.Class == "cleric";
                var hurt = s.State.Party.Where(p => p.IsAlive && p.Hp < p.MaxHp / 2).OrderBy(p => p.Hp).FirstOrDefault();
                CombatAction action;
                if (cleric && hurt is not null && c.Sp >= 1)
                {
                    action = new CombatAction(CombatActionKind.Cast, Ally: s.State.Party.IndexOf(hurt), SpellId: "c_heal_light");
                }
                else if (c.Class == "sorcerer" && c.Sp >= 2)
                {
                    action = new CombatAction(CombatActionKind.Cast, SpellId: "s_sleep", Target: combat.Monsters.FindIndex(m => m.IsActive && m.Def.Id == "kobold"));
                }
                else
                {
                    action = combat.IsInFrontRank(c) ? new CombatAction(CombatActionKind.Attack)
                        : s.Rules.HasMissileWeapon(c) ? new CombatAction(CombatActionKind.Shoot)
                        : new CombatAction(CombatActionKind.Block);
                }
                combat.Act(action);
            }
            if (combat.Outcome == CombatOutcome.Victory)
            {
                wins++;
            }
        }
        Assert.True(wins >= 6, $"A lightly trained quick party won only {wins}/10 chieftain fights.");
    }
}
