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
}
