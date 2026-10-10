using AVAMMB1.Core.Content;
using AVAMMB1.Core.Session;

namespace AVAMMB1.Tests;

/// <summary>Pressure plates, levers and gates, riddles and choices.</summary>
public class PuzzleTests
{
    private static GameSession In(string map)
    {
        var s = TestContent.StartedSession();
        var entry = s.Content.Map(map).AllEvents.First(e => e.Type == MapEventKind.Teleport);
        (s.State.MapId, s.State.X, s.State.Y) = (map, entry.X, entry.Y);
        return s;
    }

    [Fact]
    public void PressurePlate_OpensTheHiddenAlcove()
    {
        var s = In("cellars");
        var walker = new Walker(s) { Overwhelm = true };
        Assert.Throws<InvalidOperationException>(() => walker.Go(14, 4)); // a wall of fitted stones
        walker.Go(3, 13);
        Assert.Contains("cellar_alcove_open", s.State.Flags);
        var gold = s.State.Gold;
        walker.Go(14, 4);
        Assert.Equal((14, 4), (s.State.X, s.State.Y));
        Assert.True(s.State.Gold > gold);
    }

    [Fact]
    public void Lever_RaisesThePortcullis()
    {
        var s = In("mines1");
        var walker = new Walker(s) { Overwhelm = true };
        Assert.Throws<InvalidOperationException>(() => walker.Go(15, 14));
        walker.Go(2, 4);
        walker.Go(15, 14);
        Assert.Equal((15, 14), (s.State.X, s.State.Y));
        Assert.Contains("mines1_gate_cache", s.State.CompletedEvents);
    }

    [Theory]
    [InlineData("A map!", "map")]
    [InlineData("  the   Silence. ", "silence")]
    [InlineData("an Echo", "echo")]
    public void Answers_IgnoreCaseArticlesAndPunctuation(string typed, string expected) =>
        Assert.Equal(expected, GameSession.NormalizeAnswer(typed));

    [Fact]
    public void Riddle_WrongAnswerHurts_RightAnswerOpensTheDoor()
    {
        var s = In("tomb1");
        var walker = new Walker(s) { Overwhelm = true };
        var riddle = walker.Go(1, 12).Interaction;
        Assert.Equal(MapEventKind.Riddle, riddle?.Type);

        var hp = s.State.Party.Sum(c => c.Hp);
        var (wrong, _) = s.AnswerRiddle(riddle!, "a sword");
        Assert.False(wrong);
        Assert.True(s.State.Party.Sum(c => c.Hp) < hp);

        var gold = s.State.Gold;
        var (right, log) = s.AnswerRiddle(riddle!, "A map.");
        Assert.True(right);
        Assert.True(s.State.Gold > gold);
        Assert.Contains(s.State.Party, c => c.Backpack.Any(i => i.ItemId == "scarab_amulet"));
        Assert.Contains("tomb1_riddle", s.State.CompletedEvents);
        Assert.False(s.AnswerRiddle(riddle!, "map").Correct); // solved once only
        Assert.NotEmpty(log);
    }

    [Fact]
    public void Choice_AppliesOnlyTheChosenOption_Once()
    {
        var s = TestContent.StartedSession();
        s.State.Flags.Add("choir_saltreach");
        (s.State.MapId, s.State.X, s.State.Y) = ("duskmere", 4, 3);
        var choice = s.Interact().Interaction!;
        var gold = s.State.Gold;
        s.Choose(choice, 1); // hand her over
        Assert.Contains("veyl_reported", s.State.Flags);
        Assert.DoesNotContain("veyl_spared", s.State.Flags);
        Assert.Equal(gold + 600, s.State.Gold);
        Assert.Empty(s.Choose(choice, 0)); // already decided
        Assert.DoesNotContain("veyl_spared", s.State.Flags);
    }
}
