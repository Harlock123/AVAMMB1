using AVAMMB1.Core.Content;
using AVAMMB1.Core.Persistence;
using AVAMMB1.Core.Session;

namespace AVAMMB1.Tests;

/// <summary>Personal gold and the party purse.</summary>
public class GoldTests
{
    private static GameSession Fresh()
    {
        var s = TestContent.StartedSession();
        s.State.Gold = 100;
        for (var i = 0; i < s.State.Party.Count; i++)
        {
            s.State.Party[i].Gold = 10 * (i + 1); // 10, 20, ... 60
        }
        return s;
    }

    [Fact]
    public void NewGame_PoolsStartingGoldIntoThePurse()
    {
        var s = TestContent.StartedSession();
        Assert.Equal(6 * TestContent.Content.Config.StartingGoldPerMember, s.State.Gold);
        Assert.All(s.State.Party, c => Assert.Equal(0, c.Gold));
    }

    [Fact]
    public void Paying_UsesThePurseFirst_ThenThePayer()
    {
        var s = Fresh();
        var payer = s.State.Party[2]; // 30 gold
        Assert.Equal(130, s.State.Available(payer));
        Assert.True(s.State.TryPay(120, payer));
        Assert.Equal(0, s.State.Gold);
        Assert.Equal(10, payer.Gold);
        Assert.Equal(10, s.State.Party[0].Gold); // nobody else paid
    }

    [Fact]
    public void PartyWideCosts_LetEveryoneChipIn_InMarchingOrder()
    {
        var s = Fresh();
        Assert.Equal(100 + 210, s.State.Available(null));
        Assert.True(s.State.TryPay(125));
        Assert.Equal(0, s.State.Gold);
        Assert.Equal(0, s.State.Party[0].Gold);   // 10
        Assert.Equal(5, s.State.Party[1].Gold);   // 20 -> paid 15
        Assert.Equal(30, s.State.Party[2].Gold);
    }

    [Fact]
    public void FailedPayment_TakesNothing()
    {
        var s = Fresh();
        Assert.False(s.State.TryPay(1000));
        Assert.Equal(100, s.State.Gold);
        Assert.Equal(210, s.State.Party.Sum(c => c.Gold));
    }

    [Fact]
    public void DepositWithdrawPoolAndShare()
    {
        var s = Fresh();
        var c = s.State.Party[0];
        Assert.Equal(10, s.State.Deposit(c, 50)); // clamped to what they carry
        Assert.Equal(110, s.State.Gold);
        Assert.Equal(110, s.State.Withdraw(c, 500)); // clamped to the purse
        Assert.Equal(0, s.State.Gold);
        Assert.Equal(110, c.Gold);

        s.State.PoolAll();
        Assert.Equal(310, s.State.Gold);
        Assert.All(s.State.Party, m => Assert.Equal(0, m.Gold));
        Assert.Equal(51, s.State.ShareEvenly());
        Assert.Equal(4, s.State.Gold); // 310 - 6 * 51
        Assert.Equal(310, s.State.TotalGold);
    }

    [Fact]
    public void LeavingAtTheInn_TakesGoldAlong_AndBringsItBack()
    {
        var s = Fresh();
        var leaver = s.State.Party[5]; // 60 gold
        s.Town.LeaveAtInn(5, takeFromPurse: 40);
        Assert.DoesNotContain(leaver, s.State.Party);
        Assert.Equal(100, leaver.Gold);
        Assert.Equal(60, s.State.Gold);
        Assert.Equal(60 + 150, s.State.TotalGold); // the leaver's gold no longer counts for the party

        s.Town.JoinParty(s.State.Roster.IndexOf(leaver));
        Assert.Contains(leaver, s.State.Party);
        Assert.Equal(100, leaver.Gold);
        Assert.Equal(60 + 250, s.State.TotalGold);
    }

    [Fact]
    public void Shopping_LetsTheBuyerTopUpFromTheirOwnGold()
    {
        var s = Fresh();
        var buyer = s.State.Party[0];
        buyer.Gold = 60;
        s.Town.Buy(s.Content.Shops["brindle_smithy"], "chain_mail", buyer); // 150 = 100 purse + 50 own
        Assert.Contains(buyer.Backpack, i => i.ItemId == "chain_mail");
        Assert.Equal(0, s.State.Gold);
        Assert.Equal(10, buyer.Gold);

        var sold = s.Town.Sell(buyer, buyer.Backpack.FindIndex(i => i.ItemId == "chain_mail"));
        Assert.Equal(75, s.State.Gold); // sales go to the purse
    }

    [Fact]
    public void RecruitsArriveWithTheirOwnGold()
    {
        var s = TestContent.StartedSession();
        s.Town.LeaveAtInn(5);
        var recruit = TestContent.Make(s, "robber");
        s.Town.Recruit(recruit);
        Assert.Equal(TestContent.Content.Config.StartingGoldPerMember, recruit.Gold);
    }

    [Fact]
    public void PersonalGold_SurvivesSaveAndLoad()
    {
        var s = Fresh();
        s.Town.LeaveAtInn(0, 25);
        var json = SaveGameService.Serialize(new SaveFile { State = s.State });
        var loaded = SaveGameService.Deserialize(json).State;
        Assert.Equal(35, loaded.Roster[0].Gold);
        Assert.Equal(20, loaded.Party[0].Gold);
        Assert.Equal(75, loaded.Gold);
    }

    [Fact]
    public void Bribes_CanUseEveryonesGold()
    {
        var s = Fresh();
        s.State.Gold = 0;
        var start = new StepResult();
        s.StartCombat(AVAMMB1.Core.Combat.CombatEngine.Spawn(s.Content.Monster("kobold"), 1, s.Random), start);
        var cost = s.Combat!.BribeCost!.Value;
        Assert.True(cost <= 210);
        var engine = new AVAMMB1.Core.Combat.CombatEngine(s.Rules, new ScriptedRandom(0), s.State, s.Combat.Monsters);
        engine.TryBribe();
        Assert.Equal(AVAMMB1.Core.Combat.CombatOutcome.Bribed, engine.Outcome);
        Assert.Equal(210 - cost, s.State.Party.Sum(c => c.Gold));
    }
}
