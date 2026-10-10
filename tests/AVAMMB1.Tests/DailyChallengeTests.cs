using AVAMMB1.Core.Content;
using AVAMMB1.Core.Dice;
using AVAMMB1.Core.Persistence;
using AVAMMB1.Core.Session;
using AVAMMB1.Core.World;

namespace AVAMMB1.Tests;

/// <summary>The daily Depths challenge.</summary>
[Collection(DepthsCollection.Name)]
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

    [Fact]
    public void TheResultCard_SumsUpTheRun()
    {
        var s = Start("2026-10-10", 1);
        s.State.DeepestDepth = 7;
        s.State.Depth = 5;
        s.State.Kills["ghoul"] = 12;
        s.State.Kills["wight"] = 3;
        s.State.Count(Chronicle.Keys.BattlesWon, 6);
        s.State.Steps = 412;
        s.State.PlaySeconds = 42 * 60;
        s.State.Party[1].Hp = 0;
        s.State.Party[1].Conditions |= AVAMMB1.Core.Rules.Condition.Dead;

        var fell = DailyCard.Text(s.State, s.Content, climbedOut: false);
        Assert.StartsWith("AVAM&M Daily Challenge 2026-10-10\nFell on level 5 (deepest 7)\n▼▼▼▼▼▼▼\n", fell, StringComparison.Ordinal);
        Assert.Contains("♥✝♥♥♥♥ 5 of 6 standing", fell, StringComparison.Ordinal);
        Assert.Contains("Foes beaten 15 · battles 6 · chests 0 · steps 412 · 0:42", fell, StringComparison.Ordinal);
        Assert.Contains(s.State.Party[0].Name + " (Knight 15)", fell, StringComparison.Ordinal);

        Assert.Contains("Climbed out from level 7", DailyCard.Text(s.State, s.Content, climbedOut: true), StringComparison.Ordinal);
    }

    [Fact]
    public void TheTitleScreen_ShowsToday_Yesterday_AndTheBest()
    {
        var hof = new HallOfFame();
        Assert.Null(DailyCard.Records(hof, "2026-10-10"));
        hof.RecordDaily("2026-10-01", 12);
        hof.RecordDaily("2026-10-09", 9);
        Assert.Equal("Today: not tried yet · yesterday: level 9 · best: level 12 (2026-10-01)", DailyCard.Records(hof, "2026-10-10"));
        hof.RecordDaily("2026-10-10", 4);
        Assert.Equal("Today: level 4 · yesterday: level 9 · best: level 12 (2026-10-01)", DailyCard.Records(hof, "2026-10-10"));
        Assert.Equal("Today: not tried yet · best: level 12 (2026-10-01)", DailyCard.Records(hof, "2026-10-20"));
    }
}
