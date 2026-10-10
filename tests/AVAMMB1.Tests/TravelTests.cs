using AVAMMB1.Core.Session;

namespace AVAMMB1.Tests;

/// <summary>The travel map between known towns.</summary>
public class TravelTests
{
    private static GameSession InBrindlemoor(int seed = 3, params string[] visited)
    {
        var s = TestContent.StartedSession(seed);
        foreach (var m in visited)
        {
            s.State.Explored[m] = "1";
        }
        s.State.Gold = 10_000;
        return s;
    }

    [Fact]
    public void Options_ListOnlyVisitedTowns_WithTheirCost()
    {
        var s = InBrindlemoor(3, "saltreach", "wintermere");
        var options = s.TravelOptions();
        Assert.Equal(["saltreach", "wintermere"], options.Select(o => o.MapId).Order());
        var salt = options.Single(o => o.MapId == "saltreach");
        Assert.True(salt.Steps > 10);
        Assert.Equal(0, salt.Fare);
        var winter = options.Single(o => o.MapId == "wintermere");
        Assert.Equal(200, winter.Fare); // the ship north from Saltreach
        Assert.True(winter.Steps > salt.Steps);
        Assert.Contains("The Greenvale Wilds", salt.Via);
    }

    [Fact]
    public void Travelling_TakesTimeAndFares_AndArrivesInTown()
    {
        for (var seed = 1; seed < 40; seed++)
        {
            var s = InBrindlemoor(seed, "saltreach", "wintermere");
            var (minutes, gold) = (s.State.Minutes, s.State.Gold + s.State.Party.Sum(c => c.Gold));
            var option = s.TravelOptions().Single(o => o.MapId == "wintermere");
            var r = s.TravelTo("wintermere");
            if (r.CombatStarted)
            {
                continue; // ambushed on the road this time; try another day
            }
            Assert.Equal("wintermere", s.State.MapId);
            Assert.True(s.State.Minutes - minutes >= option.Steps * GameState.MinutesPerStep - 10);
            Assert.Equal(gold - 200, s.State.Gold + s.State.Party.Sum(c => c.Gold));
            Assert.Contains(r.Messages, m => m.Text.Contains("arrives in Wintermere", StringComparison.Ordinal));
            return;
        }
        Assert.Fail("Every journey was ambushed.");
    }

    [Fact]
    public void Ambushes_HappenInTheOpenCountry_AndEndTheJourneyThere()
    {
        var ambushed = 0;
        for (var seed = 1; seed < 40; seed++)
        {
            var s = InBrindlemoor(seed, "thornwick");
            var r = s.TravelTo("thornwick");
            if (r.CombatStarted)
            {
                ambushed++;
                Assert.Equal(AVAMMB1.Core.Content.MapKind.Outdoor, s.CurrentMap.Def.Kind);
                Assert.NotNull(s.Combat);
            }
        }
        Assert.InRange(ambushed, 1, 38);
    }

    [Fact]
    public void NoTravel_FromDungeons_OrToUnknownPlaces()
    {
        var s = InBrindlemoor(3, "saltreach");
        Assert.DoesNotContain(s.TravelOptions(), o => o.MapId == "thornwick");
        (s.State.MapId, s.State.X, s.State.Y) = ("cellars", 1, 14);
        Assert.False(s.CanTravel);
        Assert.Empty(s.TravelOptions());
        Assert.False(s.TravelTo("saltreach").Moved);
    }
}
