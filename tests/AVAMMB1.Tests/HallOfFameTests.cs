using AVAMMB1.Core.Dice;
using AVAMMB1.Core.Persistence;
using AVAMMB1.Core.Rules;
using AVAMMB1.Core.Session;

namespace AVAMMB1.Tests;

/// <summary>The Hall of Fame and ironman runs.</summary>
public class HallOfFameTests
{
    private static string TempDir() => Path.Combine(Path.GetTempPath(), "avammb1-tests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Store_KeepsRunsAndAchievementsAcrossGames()
    {
        var dir = TempDir();
        try
        {
            var store = new HallOfFameStore(Path.Combine(dir, "halloffame.json"));
            Assert.Empty(store.Load().Entries); // a missing file is an empty hall

            var s = TestContent.StartedSession();
            s.State.Difficulty = Difficulty.Hard;
            s.State.Ironman = true;
            s.State.Achievements.Add("first_blood");
            s.State.Kills["kobold"] = 12;
            var hof = store.Load();
            hof.Add(HallOfFameEntry.From(s.State, s.Content, "Fell in Brindlemoor Cellars"));
            hof.Achievements.Add("wayfarer");
            store.Save(hof);

            var back = store.Load();
            var e = Assert.Single(back.Entries);
            Assert.Equal("Fell in Brindlemoor Cellars", e.Outcome);
            Assert.True(e.Ironman);
            Assert.Equal(Difficulty.Hard, e.Difficulty);
            Assert.Equal(12, e.MonstersSlain);
            Assert.Equal(6, e.Party.Count);
            Assert.Contains("Human Knight 1", e.Party[0], StringComparison.Ordinal);
            Assert.Equal(["first_blood", "wayfarer"], back.Achievements.Order());

            File.WriteAllText(store.Path, "{ not json");
            Assert.Empty(store.Load().Entries); // a damaged file never stops the game
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Hall_KeepsTheNewestHundredRuns()
    {
        var hof = new HallOfFame();
        for (var i = 0; i < 105; i++)
        {
            hof.Add(new HallOfFameEntry { Outcome = "run " + i });
        }
        Assert.Equal(100, hof.Entries.Count);
        Assert.Equal("run 104", hof.Entries[0].Outcome);
    }

    [Fact]
    public void IronmanSlot_IsListedOnlyWhileARunIsInProgress()
    {
        var dir = TempDir();
        try
        {
            var saves = new SaveGameService(dir);
            var s = TestContent.StartedSession();
            Assert.DoesNotContain(saves.List(), x => x.IsIronman);
            saves.Save(SaveGameService.IronmanSlot, "Ironman", "here", s.State, [1, 2, 3]);
            var info = Assert.Single(saves.List(), x => x.IsIronman);
            Assert.False(info.IsAuto);
            Assert.Equal(SaveGameService.IronmanSlot, saves.MostRecentSlot());
            saves.Delete(SaveGameService.IronmanSlot);
            Assert.DoesNotContain(saves.List(), x => x.IsIronman);
            Assert.False(File.Exists(saves.ThumbnailFor(SaveGameService.IronmanSlot)));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void NewGame_CanBeIronman_AndWinningItEarnsIronWill()
    {
        var s = new GameSession(TestContent.Content, new DefaultRandomSource(2)) { Ironman = true };
        s.NewGame(TestContent.Content.Config.Premades.Select(s.Factory.CreatePremade));
        Assert.True(s.State.Ironman);
        s.State.Won = true;
        Assert.Contains(Chronicle.Check(s), m => m.Text.Contains("Iron Will", StringComparison.Ordinal));
    }
}
