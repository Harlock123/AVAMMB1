using AVAMMB1.Core.Content;
using AVAMMB1.Core.Rules;
using AVAMMB1.Core.Session;

namespace AVAMMB1.Tests;

/// <summary>Towns by time of day: the night market, thieves after dark, dawn prayers, the rumour of the day.</summary>
public class TownLifeTests
{
    private static GameSession At(string town, int hour, int seed = 4)
    {
        var s = TestContent.StartedSession(seed);
        s.State.MapId = town;
        s.State.AdvanceTo(hour * 60);
        return s;
    }

    [Fact]
    public void NightMarket_OpensOnlyAfterDark()
    {
        var day = At("ashkar", 12);
        (day.State.X, day.State.Y) = (10, 13);
        var r = day.Interact();
        Assert.Null(r.Interaction);
        Assert.Contains(r.Messages, m => m.Text.Contains("only come out after dark", StringComparison.Ordinal));

        var night = At("ashkar", 22);
        (night.State.X, night.State.Y) = (10, 13);
        Assert.Equal("ashkar_night_market", night.Interact().Interaction?.Shop);

        // Other shops still close at night.
        var smithy = night.Content.Map("ashkar").AllEvents.First(e => e.Type == MapEventKind.Shop && e.Shop == "ashkar_bazaar");
        (night.State.X, night.State.Y) = (smithy.X, smithy.Y);
        Assert.Null(night.Interact().Interaction);
    }

    [Fact]
    public void Thieves_WorkTheStreetsAtNight_NotByDay()
    {
        int Fights(int hour)
        {
            var fights = 0;
            for (var seed = 1; seed <= 15; seed++)
            {
                var s = At("brindlemoor", hour, seed);
                (s.State.X, s.State.Y, s.State.Facing) = (1, 14, Direction.East);
                for (var i = 0; i < 12; i++)
                {
                    var r = s.Move(i % 2 == 0 ? MoveKind.Forward : MoveKind.Back);
                    if (r.CombatStarted)
                    {
                        Assert.Contains(s.Combat!.Monsters, m => m.Def.Id == "cutpurse");
                        fights++;
                        break;
                    }
                }
            }
            return fights;
        }
        Assert.Equal(0, Fights(12));
        Assert.True(Fights(23) > 0);
    }

    [Fact]
    public void DawnPrayers_CostAQuarterLess()
    {
        var c = TestContent.StartedSession().State.Party[0];
        c.Hp = 1;
        int CostAt(int hour)
        {
            var s = At("brindlemoor", hour);
            var temple = s.Content.Map("brindlemoor").AllEvents.First(e => e.Type == MapEventKind.Temple);
            return s.Town.TempleCost(c, temple);
        }
        Assert.Equal(CostAt(12) * 3 / 4, CostAt(6), 1.0);
        Assert.True(At("brindlemoor", 6).Town.DawnPrayers);
        Assert.False(At("brindlemoor", 9).Town.DawnPrayers);
    }

    [Fact]
    public void Taverns_HaveARumourOfTheDay()
    {
        var s = At("brindlemoor", 12);
        s.State.Gold = 1000;
        var tavern = s.Content.Map("brindlemoor").AllEvents.First(e => e.Type == MapEventKind.Tavern);
        var first = s.Town.BuyDrinks(tavern)[^1].Text;
        Assert.Equal(first, s.Town.BuyDrinks(tavern)[^1].Text); // the same talk all day
        var heard = new HashSet<string> { first };
        for (var day = 0; day < 10; day++)
        {
            s.State.Minutes += GameState.MinutesPerDay;
            heard.Add(s.Town.BuyDrinks(tavern)[^1].Text);
        }
        Assert.True(heard.Count > 3, "the rumours should change from day to day");
    }
}
