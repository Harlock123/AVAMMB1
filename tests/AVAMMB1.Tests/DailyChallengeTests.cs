using AVAMMB1.Core.Content;
using AVAMMB1.Core.Dice;
using AVAMMB1.Core.Persistence;
using AVAMMB1.Core.Session;
using AVAMMB1.Core.World;

namespace AVAMMB1.Tests;

/// <summary>The daily Depths challenge.</summary>
public class DailyChallengeTests
{
    private static GameSession Start(string date, int seed)
    {
        var s = new GameSession(TestContent.Content, new DefaultRandomSource(seed));
        s.StartDailyChallenge(date, TestContent.Content.Config.Premades.Select(s.Factory.CreatePremade));
        return s;
    }

    [Fact]
    public void StartsAPartyAtLevelFifteen_InTheDaysDepths()
    {
        var s = Start("2026-10-10", 1);
        Assert.Equal(Depths.MapId, s.State.MapId);
        Assert.Equal(1, s.State.Depth);
        Assert.All(s.State.Party, c => Assert.Equal(GameSession.DailyChallengeLevel, c.Level));
        Assert.True(s.State.Ironman);
        Assert.Equal(AVAMMB1.Core.Rules.Difficulty.Normal, s.State.Difficulty);
        Assert.Equal(Depths.DailySeed("2026-10-10", 1), s.State.DepthSeed);
    }

    [Fact]
    public void EveryoneGetsTheSameLevelsOnTheSameDay()
    {
        var grid = Start("2026-10-10", 1).CurrentMap.Def.Grid;
        Assert.Equal(grid, Start("2026-10-10", 99).CurrentMap.Def.Grid); // another player, another random source
        Assert.NotEqual(grid, Start("2026-10-11", 1).CurrentMap.Def.Grid);
        Assert.Equal(Depths.DailySeed("2026-10-10", 3), Depths.DailySeed("2026-10-10", 3));
    }

    [Fact]
    public void ClimbingOut_EndsTheRun()
    {
        var s = Start("2026-10-10", 1);
        var walker = new Walker(s) { Overwhelm = true };
        walker.GoTo(e => e.Type == MapEventKind.Teleport && e.Map == Depths.MapId);
        Assert.Equal(2, s.State.Depth);
        var up = walker.GoTo(e => e.Type == MapEventKind.Teleport && e.Map == Depths.EntranceMap);
        Assert.True(up.ChallengeOver);
        Assert.Equal(Depths.MapId, s.State.MapId); // the run ends where it is
        Assert.Equal(2, s.State.DeepestDepth);
    }

    [Fact]
    public void HallOfFame_KeepsEachDaysBest()
    {
        var hof = new HallOfFame();
        Assert.True(hof.RecordDaily("2026-10-10", 4));
        Assert.False(hof.RecordDaily("2026-10-10", 3));
        Assert.True(hof.RecordDaily("2026-10-10", 6));
        Assert.Equal(6, hof.DailyBest["2026-10-10"]);
    }
}
