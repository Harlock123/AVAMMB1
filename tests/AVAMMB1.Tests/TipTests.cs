using AVAMMB1.Core.Content;
using AVAMMB1.Core.Items;
using AVAMMB1.Core.Persistence;
using AVAMMB1.Core.Session;

namespace AVAMMB1.Tests;

/// <summary>The guided first hour.</summary>
public class TipTests
{
    private static string? Next(GameSession s, TipMoment moment, List<string> seen, StepResult? r = null) =>
        Tips.Next(s, r, moment, seen)?.Id;

    [Fact]
    public void TheWelcome_ComesFirst_ThenEachTipOnlyOnce()
    {
        var s = TestContent.StartedSession();
        var seen = new List<string>();
        Assert.Equal("welcome", Next(s, TipMoment.Exploring, seen));
        seen.Add("welcome");
        Assert.Null(Next(s, TipMoment.Exploring, seen)); // nothing else has come up yet in town
        Assert.Equal("battle", Next(s, TipMoment.Battle, seen));
        seen.Add("battle");
        Assert.Null(Next(s, TipMoment.Battle, seen));
    }

    [Fact]
    public void Tips_FollowWhatHappens()
    {
        var s = TestContent.StartedSession();
        var seen = new List<string> { "welcome" };

        var shop = s.Content.Map("brindlemoor").AllEvents.First(e => e.Type == MapEventKind.Shop);
        Assert.Equal("shop", Next(s, TipMoment.Building, seen, new StepResult { Interaction = shop }));

        var locked = new StepResult();
        locked.Messages.Add(new GameMessage("The door is locked tight."));
        Assert.Equal("locked", Next(s, TipMoment.Exploring, seen, locked));

        var loot = new StepResult();
        loot.Messages.Add(new GameMessage("Found 20 gold.", MessageKind.Loot));
        Assert.Equal("loot", Next(s, TipMoment.Exploring, seen, loot));

        s.State.Flags.Add("pell_met");
        Assert.Equal("quest", Next(s, TipMoment.Exploring, seen));
        seen.Add("quest");

        s.State.MapId = "cellars";
        Assert.Equal("dungeon", Next(s, TipMoment.Exploring, seen));
        seen.Add("dungeon");

        s.State.Party[0].Hp = 1;
        Assert.Equal("wounded", Next(s, TipMoment.Exploring, seen));
        seen.Add("wounded");

        s.State.Party[1].Experience = 1_000_000;
        Assert.Equal("levelup", Next(s, TipMoment.Exploring, seen));
    }

    [Fact]
    public void NoTips_InTheDailyChallenge()
    {
        var s = TestContent.StartedSession();
        s.State.DailyChallenge = "2026-10-10";
        Assert.Null(Next(s, TipMoment.Exploring, []));
        Assert.Null(Next(s, TipMoment.Battle, []));
    }

    [Fact]
    public void KeysInTips_AreThePlayersOwn()
    {
        var bindings = GameSettings.DefaultBindings();
        Assert.Equal("Press ↑ or J; M; Esc", Tips.WithKeys("Press {MoveForward} or {Journal}; {Automap}; {Menu}", bindings));
        bindings[InputAction.Journal] = ["Q"];
        bindings[InputAction.Automap] = [];
        Assert.Equal("Q (unbound)", Tips.WithKeys("{Journal} {Automap}", bindings));
    }

    [Fact]
    public void EveryTip_HasAUniqueId_AndOnlyKnownKeys()
    {
        Assert.Equal(Tips.All.Count, Tips.All.Select(t => t.Id).Distinct().Count());
        foreach (var tip in Tips.All)
        {
            var text = Tips.WithKeys(tip.Text, GameSettings.DefaultBindings());
            Assert.DoesNotContain("{", text, StringComparison.Ordinal);
            Assert.DoesNotContain("(unbound)", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Tips_AreOnForNewPlayers_AndRemembered()
    {
        var s = new GameSettings();
        Assert.Null(s.ShowTips); // decided at start: on for a fresh install
        s.ShowTips = true;
        s.SeenTips.Add("welcome");
        var back = System.Text.Json.JsonSerializer.Deserialize(
            System.Text.Json.JsonSerializer.Serialize(s, GameJsonContext.Default.GameSettings), GameJsonContext.Default.GameSettings)!;
        Assert.True(back.ShowTips);
        Assert.Equal(["welcome"], back.SeenTips);
    }
}
