using AVAMMB1.Core.Content;
using AVAMMB1.Core.Items;
using AVAMMB1.Core.Session;

namespace AVAMMB1.Tests;

/// <summary>The clock, night outdoors, opening hours and night encounters.</summary>
public class DayNightTests
{
    private static void At(GameSession s, string mapId, int x, int y)
    {
        (s.State.MapId, s.State.X, s.State.Y) = (mapId, x, y);
    }

    [Fact]
    public void Clock_StartsAtEight_StepsTakeThreeMinutes_RestEightHours_InnUntilSeven()
    {
        var s = TestContent.StartedSession();
        Assert.Equal("08:00", s.State.ClockText);
        Assert.Equal(1, s.State.Day);
        Assert.Equal("day", s.State.PartOfDay);
        s.TurnRight();
        for (var i = 0; i < 4 && !s.Move(MoveKind.Forward).Moved; i++)
        {
            s.TurnRight();
        }
        Assert.Equal("08:03", s.State.ClockText);

        // Resting outside a dungeon: eight hours.
        At(s, "brindlemoor", 7, 14);
        var before = s.State.Minutes;
        s.Rest();
        Assert.Equal(before + 8 * 60, s.State.Minutes);

        var inn = s.Content.Map("brindlemoor").AllEvents.First(e => e.Type == MapEventKind.Inn);
        s.State.Minutes = 13 * 60; // 21:00 on day 1
        Assert.True(s.State.IsNight);
        s.Town.StayAtInn(inn);
        Assert.Equal("07:00", s.State.ClockText);
        Assert.Equal(2, s.State.Day);
        Assert.False(s.State.IsNight);
    }

    [Fact]
    public void Darkness_RampsAtDuskAndDawn()
    {
        var st = new GameState();
        double At(int hour, int minute = 0) { st.Minutes = (hour * 60 + minute - GameState.StartMinuteOfDay + GameState.MinutesPerDay) % GameState.MinutesPerDay; return st.Darkness; }
        Assert.Equal(0, At(12));
        Assert.Equal(0.5, At(19), 3);
        Assert.Equal(1, At(23));
        Assert.Equal(1, At(3));
        Assert.Equal(0.5, At(6), 3);
        At(18, 30);
        Assert.Equal("dusk", st.PartOfDay);
        At(5, 30);
        Assert.Equal("dawn", st.PartOfDay);
    }

    [Fact]
    public void Night_ShortensSightOutdoors_UnlessYouCarryLight()
    {
        var s = TestContent.StartedSession();
        At(s, "wilds", 16, 16);
        Assert.Equal(10, s.ViewDistance);
        s.State.Minutes = 15 * 60; // 23:00
        Assert.Equal(4, s.ViewDistance);
        var c = s.State.Party[0];
        c.Backpack.Add(new ItemInstance("lantern", 1500));
        s.Inventory.Equip(c, c.Backpack.Count - 1);
        Assert.Equal(8, s.ViewDistance);

        // The lantern burns outdoors at night, but not in town (street lights).
        var lantern = c.Equipment[AVAMMB1.Core.Rules.EquipSlot.Light];
        s.Rest();
        Assert.True(lantern.Charges < 1500 || s.Combat is not null);
    }

    [Fact]
    public void Shops_CloseAtNight_InnTempleAndTavernStayOpen()
    {
        var s = TestContent.StartedSession();
        var town = s.Content.Map("brindlemoor");
        var shop = town.AllEvents.First(e => e.Type == MapEventKind.Shop);
        var temple = town.AllEvents.First(e => e.Type == MapEventKind.Temple);
        StepResult Visit(MapEventDef ev)
        {
            At(s, "brindlemoor", ev.X, ev.Y);
            return s.Interact();
        }
        Assert.Same(shop, Visit(shop).Interaction);
        s.State.Minutes = 14 * 60; // 22:00
        var closed = Visit(shop);
        Assert.Null(closed.Interaction);
        Assert.Contains(closed.Messages, m => m.Text.Contains("closed for the night", StringComparison.Ordinal));
        Assert.Same(temple, Visit(temple).Interaction);
    }

    [Fact]
    public void Night_BringsTheNightEncountersToTheWilds()
    {
        var wilds = TestContent.Content.Map("wilds");
        Assert.NotEmpty(wilds.Def.NightEncounters);
        var nightOnly = wilds.Def.NightEncounters.Select(e => e.Monster).Except(wilds.Def.Encounters.Select(e => e.Monster)).ToHashSet();
        Assert.NotEmpty(nightOnly);
        var seen = new HashSet<string>();
        for (var seed = 0; seed < 60 && !seen.Overlaps(nightOnly); seed++)
        {
            var s = TestContent.StartedSession(seed);
            At(s, "wilds", 16, 16);
            s.State.Minutes = 15 * 60;
            for (var i = 0; i < 40 && s.Combat is null; i++)
            {
                s.TurnRight();
                s.Move(MoveKind.Forward);
            }
            if (s.Combat is { } c)
            {
                seen.UnionWith(c.Monsters.Select(m => m.Def.Id));
            }
        }
        Assert.True(seen.Overlaps(nightOnly), "no night-only monster appeared");
    }

    [Fact]
    public void OldSaves_GetAClockFromTheirSteps()
    {
        var s = TestContent.StartedSession();
        var state = s.State;
        state.Steps = 1000;
        state.Minutes = 0;
        s.Load(state);
        Assert.Equal(3000, s.State.Minutes);
    }

    [Fact]
    public void Footsteps_SoundLikeTheGround()
    {
        var db = TestContent.Content;
        Assert.Equal("step_snow", GameSession.StepSound(db.Map("frostmark"), 10, 10));
        Assert.Equal("step", GameSession.StepSound(db.Map("cellars"), 1, 14));
        Assert.Equal("step_soft", GameSession.StepSound(db.Map("wilds"), 16, 16));
        var temple = db.Map("temple");
        var wet = Enumerable.Range(0, temple.Width).SelectMany(x => Enumerable.Range(0, temple.Height).Select(y => (x, y)))
            .First(p => temple.FloorTexture(p.x, p.y) == "shallow_water");
        Assert.Equal("step_water", GameSession.StepSound(temple, wet.x, wet.y));
    }
}
