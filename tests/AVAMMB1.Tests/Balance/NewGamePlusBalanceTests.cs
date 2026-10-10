using AVAMMB1.Core.Rules;
using Xunit.Abstractions;

namespace AVAMMB1.Tests.Balance;

/// <summary>
/// New Game+ pacing, measured with <see cref="BalanceSimulator"/>: a party that has just won (level 16,
/// the best gear the shops sell) against a world one cycle stronger.
/// </summary>
public class NewGamePlusBalanceTests(ITestOutputHelper output)
{
    private (double Xp, double Deaths, double Wipes, double BattlesPerLevel) At(string map, int cycle, int level = 16)
    {
        var zone = BalanceTests.Route.Single(z => z.Map == map);
        var sim = new BalanceSimulator(7);
        sim.Session.State.Cycle = cycle;
        sim.Session.State.CyclePartyLevel = 16;
        var (xp, gold, temple, deaths, wipes) = sim.Profile(zone, level, 80);
        var knight = sim.Session.Content.Class("knight");
        var per = (Rulebook.XpForLevel(knight, level + 1) - Rulebook.XpForLevel(knight, level)) / Math.Max(1, xp);
        output.WriteLine($"NG+{cycle} L{level} {map,-10} xp/battle {xp,7:F0} = {per,5:F0} battles/level  gold {gold,6:F0}  temple {temple,5:F0}  deaths {deaths:F2}  wipes {wipes:F3}");
        return (xp, deaths, wipes, per);
    }

    [Fact]
    public void TheFirstCycle_IsAChallengeAgain_ButNotAWall()
    {
        var first = At("deep", 0);
        var cellars = At("cellars", 1);
        var tomb = At("tomb1", 1);
        var deep = At("deep", 1);
        Assert.Equal(0, cellars.Wipes); // the first dungeon is a warm-up again, not a death trap
        Assert.True(deep.Deaths > first.Deaths, "the Sunless Deep should be harder in New Game+");
        Assert.True(deep.Xp > first.Xp * 1.5, "and pay a great deal more");
        Assert.True(deep.Wipes < 0.05 && tomb.Wipes < 0.05, "but a winning party should not be wiped out often");
        Assert.True(tomb.BattlesPerLevel <= 80, $"levelling in New Game+ takes too long ({tomb.BattlesPerLevel:F0} battles per level)");
    }

    [Fact]
    public void EachCycle_IsHarderThanTheLast()
    {
        var one = At("deep", 1);
        var three = At("deep", 3);
        static double Harm((double Xp, double Deaths, double Wipes, double BattlesPerLevel) r) => r.Deaths + 6 * r.Wipes; // a wipe costs the whole party
        Assert.True(Harm(three) > Harm(one) && three.Xp > one.Xp, "the third cycle should hurt more and pay more than the first");
    }
}
