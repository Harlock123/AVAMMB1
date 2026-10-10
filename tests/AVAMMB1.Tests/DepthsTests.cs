using AVAMMB1.Core.Combat;
using AVAMMB1.Core.Content;
using AVAMMB1.Core.Persistence;
using AVAMMB1.Core.Rules;
using AVAMMB1.Core.Session;
using AVAMMB1.Core.World;

namespace AVAMMB1.Tests;

/// <summary>The Depths Below: generated levels of an endless post-game dungeon.</summary>
[Collection(DepthsCollection.Name)]
public class DepthsTests
{
    [Theory]
    [InlineData(1, 11)]
    [InlineData(5, 22)]
    [InlineData(12, 33)]
    [InlineData(30, 44)]
    public void Levels_AreConnectedDeterministicAndFurnished(int depth, int seed)
    {
        var db = TestContent.Content;
        var def = Depths.Generate(db, depth, seed);
        Assert.Equal(def.Grid, Depths.Generate(db, depth, seed).Grid); // the same seed gives the same level
        Assert.NotEqual(def.Grid, Depths.Generate(db, depth, seed + 1).Grid);
        var map = GameMap.Parse(def);

        // Every square can be reached from the stair up.
        var seen = new HashSet<(int, int)> { Depths.Start };
        var q = new Queue<(int X, int Y)>([Depths.Start]);
        while (q.Count > 0)
        {
            var (x, y) = q.Dequeue();
            foreach (var d in Enum.GetValues<Direction>())
            {
                var (w, solid) = map.Probe(x, y, d);
                var n = (x + d.Dx(), y + d.Dy());
                if (w != WallKind.Wall && !solid && map.InBounds(n.Item1, n.Item2) && seen.Add(n))
                {
                    q.Enqueue(n);
                }
            }
        }
        Assert.Equal(16 * 16, seen.Count);

        Assert.Contains(def.Events, e => e.Type == MapEventKind.Teleport && e.Map == Depths.MapId);
        Assert.Contains(def.Events, e => e.Type == MapEventKind.Teleport && e.Map == Depths.EntranceMap);
        Assert.True(def.Events.Count(e => e.Type == MapEventKind.Treasure) >= 2);
        Assert.Equal(def.Events.Where(e => e.Id is not null).Select(e => e.Id).Distinct().Count(), def.Events.Count(e => e.Id is not null));
        Assert.All(def.Events.Where(e => e.Id is not null), e => Assert.StartsWith($"depths{depth}_", e.Id!, StringComparison.Ordinal));
        Assert.NotEmpty(def.Encounters);
        Assert.All(def.Encounters, e => Assert.True(db.Monsters.ContainsKey(e.Monster)));
        Assert.All(def.Events.SelectMany(e => e.Items), i => Assert.True(db.Items.ContainsKey(i), i));
    }

    [Fact]
    public void Descending_MakesANewLevel_AndASaveComesBackToTheSameOne()
    {
        var s = TestContent.StartedSession();
        var walker = new Walker(s) { Overwhelm = true };
        (s.State.MapId, s.State.X, s.State.Y) = ("deep", Depths.ReturnSquare.X, Depths.ReturnSquare.Y);
        var blocked = walker.Go(Depths.EntranceStair.X, Depths.EntranceStair.Y); // the wyrm first
        Assert.Equal("deep", s.State.MapId);
        Assert.Contains(blocked.Messages, m => m.Text.Contains("coils", StringComparison.Ordinal));
        (s.State.X, s.State.Y) = Depths.ReturnSquare;

        s.State.Flags.Add(Depths.OpenFlag);
        walker.Go(Depths.EntranceStair.X, Depths.EntranceStair.Y);
        Assert.Equal((Depths.MapId, 1), (s.State.MapId, s.State.Depth));
        Assert.Equal(Depths.Start, (s.State.X, s.State.Y));
        Assert.StartsWith("The Depths Below, level 1", s.CurrentMap.Def.Name, StringComparison.Ordinal);

        walker.GoTo(e => e.Type == MapEventKind.Teleport && e.Map == Depths.MapId);
        Assert.Equal(2, s.State.Depth);
        Assert.Equal(2, s.State.DeepestDepth);
        var grid = s.CurrentMap.Def.Grid;

        var json = SaveGameService.Serialize(new SaveFile { State = s.State });
        TestContent.Content.Map(Depths.MapId); // whatever level is loaded now
        var t = TestContent.NewSession(9);
        t.Load(SaveGameService.Deserialize(json).State);
        Assert.Equal(grid, t.CurrentMap.Def.Grid);

        walker.GoTo(e => e.Type == MapEventKind.Teleport && e.Map == Depths.EntranceMap);
        Assert.Equal("deep", s.State.MapId);
        Assert.Equal(2, s.State.DeepestDepth);
    }

    [Fact]
    public void DeeperLevels_HaveStrongerMonsters_AndPayMore()
    {
        Assert.Equal(100, Depths.ScalePercent(3));
        Assert.Equal(170, Depths.ScalePercent(10));
        var s = TestContent.StartedSession();
        s.State.MapId = Depths.MapId;
        s.State.Depth = 10;
        var m = new MonsterInstance(s.Content.Monster("frost_giant"), 100);
        s.StartCombat([m], new StepResult());
        Assert.Equal(170, m.MaxHp);
        Assert.Equal(170, m.DamagePercent);
        Assert.Equal(170, s.Combat!.RewardPercent);
    }

    [Fact]
    public void ReachingLevelTen_EarnsDeepDelver()
    {
        var s = TestContent.StartedSession();
        s.State.DeepestDepth = 10;
        Assert.Contains(Chronicle.Check(s), msg => msg.Text.Contains("Deep Delver", StringComparison.Ordinal));
    }
}
