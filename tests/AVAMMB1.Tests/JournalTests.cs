using AVAMMB1.Core.Items;
using AVAMMB1.Core.Session;

namespace AVAMMB1.Tests;

public class JournalTests
{
    [Fact]
    public void Quests_AppearAndAdvanceWithFlagsItemsAndVisits()
    {
        var s = TestContent.StartedSession();
        Assert.Empty(QuestJournal.Quests(s.State, s.Content));

        s.State.Flags.Add("pell_met");
        var main = Assert.Single(QuestJournal.Quests(s.State, s.Content));
        Assert.True(main.Main);
        Assert.Contains("Ember Shard", main.Entries[^1]);

        s.State.Party[0].Backpack.Add(new ItemInstance("ember_shard"));
        Assert.Contains("Take it back", QuestJournal.Quests(s.State, s.Content)[0].Entries[^1]);

        // Killing the Sun King before meeting Tamsin does not reveal her quest.
        s.State.Flags.Add("sun_king_slain");
        Assert.DoesNotContain(QuestJournal.Quests(s.State, s.Content), q => q.Id == "sun_disk");
        Assert.Contains(QuestJournal.Quests(s.State, s.Content), q => q.Id == "deep" && !q.Done);

        s.State.Explored["cistern"] = "1";
        Assert.Contains(QuestJournal.Quests(s.State, s.Content), q => q.Id == "cistern");
        s.State.Flags.Add("ooze_mother_dead");
        var quests = QuestJournal.Quests(s.State, s.Content);
        var cistern = quests.Single(q => q.Id == "cistern");
        Assert.True(cistern.Done);
        Assert.Equal(cistern, quests[^1]); // completed quests are listed last
    }

    [Fact]
    public void Discoveries_ListReadSignsAndHints()
    {
        var s = TestContent.StartedSession();
        Assert.Empty(QuestJournal.Discoveries(s.State, s.Content));
        s.State.CompletedEvents.Add("cellar_hint");
        var d = Assert.Single(QuestJournal.Discoveries(s.State, s.Content));
        Assert.Equal("Brindlemoor Cellars", d.Place);
        Assert.Contains("KOBOLD KING", d.Text);
    }

    [Fact]
    public void EveryQuestStage_IsSetSomewhereInTheGame()
    {
        // Every quest's flags and items exist in the game's events (no stage is unreachable).
        var db = TestContent.Content;
        var flags = db.Maps.Values.SelectMany(m => m.AllEvents).Select(e => e.SetFlag).OfType<string>().ToHashSet();
        var items = db.Maps.Values.SelectMany(m => m.AllEvents).SelectMany(e => e.Items).ToHashSet();
        foreach (var q in db.Quests)
        {
            Assert.True(q.DoneFlag is null || flags.Contains(q.DoneFlag), $"{q.Id}: done flag {q.DoneFlag} is never set");
            foreach (var st in q.Stages)
            {
                Assert.True(st.Flag is null || flags.Contains(st.Flag), $"{q.Id}: flag {st.Flag} is never set");
                Assert.True(st.Item is null || items.Contains(st.Item), $"{q.Id}: item {st.Item} is never given");
            }
        }
    }
}
