using AVAMMB1.Core.Items;
using AVAMMB1.Core.Session;

namespace AVAMMB1.Tests;

/// <summary>"Where next?" for every quest stage.</summary>
public class QuestGoalTests
{
    /// <summary>A session in which a quest has just reached a stage.</summary>
    private static GameSession AtStage(string questId, int stage)
    {
        var s = TestContent.StartedSession();
        var q = s.Content.Quests.Single(x => x.Id == questId);
        for (var j = 0; j <= stage; j++)
        {
            var st = q.Stages[j];
            if (st.Flag is not null)
            {
                s.State.Flags.Add(st.Flag);
            }
            if (st.Visited is not null)
            {
                s.State.Explored[st.Visited] = "1";
            }
        }
        if (q.Stages[stage].Item is { } item)
        {
            s.State.Party[0].Backpack.Add(new ItemInstance(item));
        }
        return s;
    }

    [Fact]
    public void EveryStageOfEveryQuest_PointsSomewhere()
    {
        var missing = new List<string>();
        foreach (var q in TestContent.Content.Quests)
        {
            for (var i = 0; i < q.Stages.Count; i++)
            {
                var goal = AtStage(q.Id, i).QuestGoals().FirstOrDefault(g => g.QuestId == q.Id);
                if (goal is null || !TestContent.Content.Map(goal.MapId).InBounds(goal.X, goal.Y))
                {
                    missing.Add($"{q.Id}[{i}]");
                }
            }
        }
        Assert.Empty(missing);
    }

    [Theory]
    [InlineData("choir", 0, "thornwick", "Loremaster's Hall")]
    [InlineData("choir", 7, "hills", "The Hollow Belfry")]             // an explicit goal in the quest data
    [InlineData("bounty_ogre", 0, "wilds", "Ogre's Den")]               // the hoard is locked: the guardian first
    [InlineData("rimefang", 1, "wintermere", "The Jarl's Hall")]        // carrying the heart: hand it in
    [InlineData("main", 0, "cellars", "Chieftain's Hall")]
    public void Goals_LeadToTheRightPlace(string quest, int stage, string map, string place)
    {
        var goal = AtStage(quest, stage).QuestGoals().Single(g => g.QuestId == quest);
        Assert.Equal((map, place), (goal.MapId, goal.Place));
    }

    [Fact]
    public void FinishedAndUnknownQuests_HaveNoGoal()
    {
        var s = TestContent.StartedSession();
        Assert.DoesNotContain(s.QuestGoals(), g => g.QuestId == "choir");
        s.State.Flags.Add("choir_met");
        s.State.Flags.Add("choir_done");
        Assert.DoesNotContain(s.QuestGoals(), g => g.QuestId == "choir");
    }
}
