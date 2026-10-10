using AVAMMB1.Core.Content;
using AVAMMB1.Core.Rules;
using AVAMMB1.Core.Session;

namespace AVAMMB1.Tests;

/// <summary>The Silent Choir: the quest chain through all six towns and the Hollow Belfry.</summary>
public class SilentChoirTests
{
    private static GameSession StrongParty()
    {
        var s = TestContent.StartedSession(seed: 11);
        foreach (var c in s.State.Party)
        {
            c.Experience = Rulebook.XpForLevel(s.Content.Class(c.Class), 14);
            while (s.Rules.LevelUp(c, s.Random) is not null) { }
            c.Hp = c.MaxHp;
        }
        return s;
    }

    /// <summary>Stands on a square and talks (as pressing Use in front of a building does).</summary>
    private static StepResult Visit(GameSession s, string map, int x, int y)
    {
        (s.State.MapId, s.State.X, s.State.Y) = (map, x, y);
        return s.Interact();
    }

    private static string Said(StepResult r) => string.Join(" ", r.Messages.Select(m => m.Text));

    private static void Place(GameSession s, string map)
    {
        var start = s.Content.Map(map).AllEvents.First(e => e.Type == MapEventKind.Teleport);
        (s.State.MapId, s.State.X, s.State.Y) = (map, start.X, start.Y);
    }

    [Fact]
    public void TheChain_RunsThroughAllSixTowns_ToTheBelfryAndBack()
    {
        var s = StrongParty();
        var walker = new Walker(s);

        // Steps out of order do nothing yet.
        Assert.Contains("buried in old maps", Said(Visit(s, "thornwick", 7, 9)), StringComparison.Ordinal);
        Assert.Contains("door is dark bronze", Said(WalkFrom(s, "hills", 16, 7, walker, 17, 7)), StringComparison.Ordinal);
        Assert.Equal("hills", s.State.MapId);

        Assert.Contains("Silent Choir", Said(Visit(s, "brindlemoor", 10, 13)), StringComparison.Ordinal);
        Assert.Contains("choir_met", s.State.Flags);
        Assert.Contains("Durin in Thornwick", Said(Visit(s, "brindlemoor", 10, 13)), StringComparison.Ordinal); // reminder

        Visit(s, "thornwick", 7, 9);
        Assert.Contains("choir_durin", s.State.Flags);

        // The bursar waits in the Deep Mines and drops his ledger.
        Place(s, "mines2");
        walker.Go(6, 10);
        Assert.True(walker.Battles > 0);
        Assert.Contains(s.State.Party, c => c.Backpack.Any(i => i.ItemId == "choir_ledger"));

        Visit(s, "thornwick", 7, 9);
        Assert.Contains("choir_ledger_read", s.State.Flags);
        Assert.DoesNotContain(s.State.Party, c => c.Backpack.Any(i => i.ItemId == "choir_ledger"));

        Visit(s, "saltreach", 6, 7);
        Assert.Contains("choir_saltreach", s.State.Flags);
        Visit(s, "duskmere", 4, 3);
        Assert.Contains("choir_veyl", s.State.Flags);
        Visit(s, "ashkar", 7, 10);
        Assert.Contains("choir_ashkar", s.State.Flags);

        // Keeper Isaura in the Tomb of the Sun Kings has the Belfry Key.
        Place(s, "tomb1");
        walker.Go(12, 12);
        Assert.Contains(s.State.Party, c => c.Backpack.Any(i => i.ItemId == "belfry_key"));

        // Now the Belfry door opens. (How hard the Choirmaster is is BossBalanceTests' business.)
        walker.Overwhelm = true;
        (s.State.MapId, s.State.X, s.State.Y) = ("hills", 16, 7);
        walker.Go(17, 7);
        Assert.Equal("belfry1", s.State.MapId);
        walker.Travel("belfry2");
        Assert.Equal("belfry2", s.State.MapId);
        walker.Go(12, 5);
        Assert.Contains("choirmaster_slain", s.State.Flags);
        Assert.Contains(s.State.Party, c => c.Backpack.Any(i => i.ItemId == "bell_clapper"));
        Assert.Contains(s.State.Party, c => c.Backpack.Any(i => i.ItemId == "bellbreaker"));

        Visit(s, "wintermere", 11, 14);
        Assert.Contains("choir_unmade", s.State.Flags);
        Assert.Contains(s.State.Party, c => c.Backpack.Any(i => i.ItemId == "ward_voices"));

        Visit(s, "brindlemoor", 10, 13);
        Assert.Contains("choir_done", s.State.Flags);
        Assert.Contains(s.State.Party, c => c.Backpack.Any(i => i.ItemId == "concord_signet"));
        var said = Said(Visit(s, "brindlemoor", 10, 13));
        Assert.Contains("singing again", said, StringComparison.Ordinal);
        Assert.DoesNotContain("Any word", said, StringComparison.Ordinal);

        var quest = QuestJournal.Quests(s.State, s.Content).Single(q => q.Id == "choir");
        Assert.True(quest.Done);
    }

    private static StepResult WalkFrom(GameSession s, string map, int x, int y, Walker walker, int tx, int ty)
    {
        (s.State.MapId, s.State.X, s.State.Y) = (map, x, y);
        return walker.Go(tx, ty);
    }

    [Fact]
    public void Journal_FollowsTheChain()
    {
        var s = TestContent.StartedSession();
        Assert.DoesNotContain(QuestJournal.Quests(s.State, s.Content), q => q.Id == "choir");
        s.State.Flags.Add("choir_met");
        var q = QuestJournal.Quests(s.State, s.Content).Single(q => q.Id == "choir");
        Assert.Contains("Loremaster Durin", q.Entries[^1], StringComparison.Ordinal);
    }
}
