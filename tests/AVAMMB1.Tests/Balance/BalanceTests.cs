using AVAMMB1.Core.Rules;
using Xunit.Abstractions;

namespace AVAMMB1.Tests.Balance;

/// <summary>
/// Progression pacing, measured by <see cref="BalanceSimulator"/> (deterministic seeds). The bounds
/// keep the grind, the wipe rate and the gold flow inside the ranges the content was tuned for; the
/// printed reports are the place to start when retuning.
/// </summary>
public class BalanceTests(ITestOutputHelper output)
{
    /// <summary>The route a typical player takes, with the level each zone (and its boss) is tuned for.</summary>
    internal static readonly Zone[] Route =
    [
        new("cellars", "brindlemoor", 3),
        new("cistern", "brindlemoor", 4),
        new("wilds", "brindlemoor", 5),
        new("crypt", "brindlemoor", 6),
        new("hills", "thornwick", 7),
        new("mines1", "thornwick", 7),
        new("mines2", "thornwick", 8),
        new("vault", "saltreach", 8),
        new("catacombs", "brindlemoor", 9),
        new("temple", "duskmere", 9),
        new("sunscar", "ashkar", 10),
        new("tomb1", "ashkar", 11),
        new("belfry1", "duskmere", 11),
        new("belfry2", "duskmere", 12),
        new("tomb2", "ashkar", 12),
        new("frostmark", "wintermere", 13),
        new("rime1", "wintermere", 13),
        new("rime2", "wintermere", 14),
        new("deep", "ashkar", 15),
    ];

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void SimulatedPlaythrough_ReachesEveryZonesLevelAtAReasonablePace(int seed)
    {
        var sim = new BalanceSimulator(seed);
        var reports = sim.Run(Route, maxBattlesPerZone: 400);
        foreach (var r in reports)
        {
            output.WriteLine(r.ToString());
        }
        foreach (var kv in sim.Outcomes.OrderByDescending(k => k.Value))
        {
            output.WriteLine($"{kv.Value,5} x {kv.Key}");
        }
        var total = reports.Sum(r => r.Battles);
        output.WriteLine($"total battles {total}, wipes {reports.Sum(r => r.Wipes)}");

        Assert.All(reports, r => Assert.True(r.Reached, $"{r.Zone.Map}: stuck at level {r.ExitLevel}"));
        Assert.All(reports.Where(r => r.Zone.Map != "deep"), r => Assert.True(r.Battles <= 120, $"{r.Zone.Map}: {r.Battles} battles"));
        Assert.True(reports.Single(r => r.Zone.Map == "deep").Battles <= 300);
        Assert.True(reports.Sum(r => r.Wipes) <= 4, "too many wipes");
        Assert.InRange(total, 350, 1000);
        Assert.DoesNotContain(sim.Outcomes.Keys, k => k.StartsWith("STUCK", StringComparison.Ordinal));
    }

    /// <summary>The whole route on Easy and Hard: both finish, Hard takes longer and costs more lives.</summary>
    [Theory]
    [InlineData(Difficulty.Easy)]
    [InlineData(Difficulty.Hard)]
    public void SimulatedPlaythrough_OnEasyAndHard(Difficulty difficulty)
    {
        var sim = new BalanceSimulator(1, difficulty);
        var reports = sim.Run(Route, maxBattlesPerZone: 400);
        foreach (var r in reports)
        {
            output.WriteLine(r.ToString());
        }
        var total = reports.Sum(r => r.Battles);
        var wipes = reports.Sum(r => r.Wipes);
        output.WriteLine($"{difficulty}: total battles {total}, wipes {wipes}, deaths {reports.Sum(r => r.Deaths)}");
        Assert.All(reports, r => Assert.True(r.Reached, $"{difficulty} {r.Zone.Map}: stuck at level {r.ExitLevel}"));
        Assert.DoesNotContain(sim.Outcomes.Keys, k => k.StartsWith("STUCK", StringComparison.Ordinal));
        if (difficulty == Difficulty.Easy)
        {
            Assert.True(wipes <= 2, $"Easy: {wipes} wipes");
            Assert.InRange(total, 300, 1000);
        }
        else
        {
            Assert.True(wipes <= 12, $"Hard: {wipes} wipes");
            Assert.InRange(total, 350, 1500);
        }
    }

    [Fact]
    public void EveryZone_PaysItsWayAndLevelsAtAReasonablePace()
    {
        foreach (var zone in Route)
        {
            var level = zone.TargetLevel - 1;
            var sim = new BalanceSimulator(7);
            var (xp, gold, temple, deaths, wipes) = sim.Profile(zone, level, 150);
            var knight = sim.Session.Content.Class("knight");
            var battlesPerLevel = (Rulebook.XpForLevel(knight, level + 1) - Rulebook.XpForLevel(knight, level)) / Math.Max(1, xp);
            output.WriteLine($"{zone.Map,-10} L{level,2}: xp/battle {xp,6:F0} = {battlesPerLevel,4:F0} battles/level  gold/battle {gold,6:F1}  temple/battle {temple,5:F1}  deaths/battle {deaths:F2}  wipes/battle {wipes:F3}");
            Assert.True(battlesPerLevel <= 80, $"{zone.Map}: {battlesPerLevel:F0} battles per level at L{level}");
            Assert.True(gold - temple > 0, $"{zone.Map}: loses money at L{level}");
            Assert.True(wipes < 0.03, $"{zone.Map}: wipes {wipes:P1} of battles at L{level}");
        }
    }

    /// <summary>The Depths Below start about as hard as the Sunless Deep and get harder - and richer - every level.</summary>
    [Fact]
    public void Depths_GetHarderAndPayMoreAsTheyGoDown()
    {
        (double Xp, double Gold, double Wipes) At(int depth)
        {
            var sim = new BalanceSimulator(7);
            sim.Session.Content.SetGeneratedMap(AVAMMB1.Core.World.Depths.Generate(sim.Session.Content, depth, 5));
            sim.Session.State.Depth = depth;
            var (xp, gold, _, _, wipes) = sim.Profile(new Zone(AVAMMB1.Core.World.Depths.MapId, "ashkar", 16), 15, 60);
            output.WriteLine($"depth {depth,2}: xp/battle {xp,6:F0}  gold/battle {gold,5:F0}  wipes/battle {wipes:F3}");
            return (xp, gold, wipes);
        }
        var deep = new BalanceSimulator(7).Profile(new Zone("deep", "ashkar", 16), 15, 60);
        var (one, five, ten) = (At(1), At(5), At(10));
        Assert.True(one.Xp >= deep.Xp * 0.8, "the first level should be at least as rewarding as the Sunless Deep");
        Assert.True(five.Xp > one.Xp && ten.Xp > five.Xp && ten.Gold > five.Gold, "deeper levels should pay more");
        Assert.True(ten.Wipes < 0.05, $"depth 10 wipes a level-15 party too often: {ten.Wipes:P1}");
    }
}
