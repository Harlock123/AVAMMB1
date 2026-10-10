using AVAMMB1.Core.Combat;
using AVAMMB1.Core.Session;

namespace AVAMMB1.Tests;

/// <summary>Statistics and achievements.</summary>
public class ChronicleTests
{
    [Fact]
    public void Battles_CountWinsGoldAndEarnFirstBlood()
    {
        var s = TestContent.StartedSession();
        Assert.Empty(Chronicle.Check(s));
        s.StartCombat(CombatEngine.Spawn(s.Content.Monster("kobold"), 2, s.Random), new StepResult());
        new Walker(s) { Overwhelm = true }.Fight();
        Assert.Equal(1, s.State.Stats[Chronicle.Keys.BattlesWon]);
        Assert.True(s.State.Stats[Chronicle.Keys.GoldFound] >= 0);

        var earned = Chronicle.Check(s);
        Assert.Contains(earned, m => m.Text.Contains("First Blood", StringComparison.Ordinal));
        Assert.Contains("first_blood", s.State.Achievements);
        Assert.Empty(Chronicle.Check(s)); // only once
    }

    [Fact]
    public void Achievements_FollowTheState()
    {
        var s = TestContent.StartedSession();
        foreach (var town in new[] { "brindlemoor", "saltreach", "thornwick", "duskmere", "ashkar", "wintermere" })
        {
            s.State.Explored[town] = "1";
        }
        s.State.Flags.Add("choir_done");
        s.State.Gold = 12_000;
        var earned = Chronicle.Check(s).Select(m => m.Text).ToList();
        Assert.Contains(earned, t => t.Contains("Wayfarer", StringComparison.Ordinal));
        Assert.Contains(earned, t => t.Contains("Friend of the Concord", StringComparison.Ordinal));
        Assert.Contains(earned, t => t.Contains("Wealthy", StringComparison.Ordinal));
        Assert.DoesNotContain(earned, t => t.Contains("Hard Won", StringComparison.Ordinal));
        Assert.True(s.State.Stats[Chronicle.Keys.MostGold] >= 12_000);
    }

    [Fact]
    public void ExploringCounts_SecretsChestsAndSpells()
    {
        var s = TestContent.StartedSession();
        var mirela = s.State.Party.First(c => s.Rules.KnownSpells(c).Any(sp => sp.Id == "c_light"));
        s.Spells.Cast(mirela, s.Content.Spell("c_light"), s.State, null, -1, null);
        Assert.Equal(1, s.State.Stats[Chronicle.Keys.Spells]);
    }

    [Fact]
    public void LoadingAnOldSave_EarnsItsAchievementsQuietly()
    {
        var s = TestContent.StartedSession();
        s.State.Flags.Add("choir_done");
        var json = AVAMMB1.Core.Persistence.SaveGameService.Serialize(new AVAMMB1.Core.Persistence.SaveFile { State = s.State });
        var t = TestContent.NewSession();
        t.Load(AVAMMB1.Core.Persistence.SaveGameService.Deserialize(json).State);
        Assert.Contains("concord", t.State.Achievements);
        Assert.Empty(Chronicle.Check(t));
    }
}
