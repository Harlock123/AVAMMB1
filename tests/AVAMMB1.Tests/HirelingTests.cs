using AVAMMB1.Core.Content;
using AVAMMB1.Core.Items;
using AVAMMB1.Core.Persistence;
using AVAMMB1.Core.Rules;
using AVAMMB1.Core.Session;

namespace AVAMMB1.Tests;

/// <summary>Adventurers for hire at the inns.</summary>
public class HirelingTests
{
    private static GameSession InTown(string town = "brindlemoor")
    {
        var s = TestContent.StartedSession();
        s.State.MapId = town;
        return s;
    }

    private static void NextDay(GameSession s)
    {
        s.State.Minutes += GameState.MinutesPerDay;
        s.Rest();
    }

    [Fact]
    public void EveryTown_HasSomeoneForHire_AndEveryHirelingCanBeMade()
    {
        var db = TestContent.Content;
        Assert.True(db.Config.Hirelings.Count >= 6);
        foreach (var town in db.Maps.Values.Where(m => m.Def.Kind == MapKind.Town))
        {
            Assert.Contains(db.Config.Hirelings, h => h.Town == town.Id);
        }
        foreach (var h in db.Config.Hirelings)
        {
            var s = InTown(h.Town);
            s.State.Gold = 10_000;
            var log = s.Hire(h.Id);
            Assert.True(log.All(m => m.Kind != MessageKind.Bad), string.Join(" ", log.Select(m => m.Text)));
            var c = s.State.Party[^1];
            Assert.Equal((h.Id, h.Level, h.Name), (c.Hireling, c.Level, c.Name));
            Assert.Equal(c.MaxHp, c.Hp);
            Assert.Equal(0, c.Gold);
        }
    }

    [Fact]
    public void Hiring_PaysTheFirstDay_AndAddsToTheSixHeroes()
    {
        var s = InTown();
        var gold = s.State.Gold;
        var tobin = s.HirelingsAt("brindlemoor").Single(h => h.Id == "tobin");
        s.Hire("tobin");
        Assert.Equal(7, s.State.Party.Count);
        Assert.Equal(gold - tobin.Wage, s.State.Gold);
        Assert.Empty(s.HirelingsAt("brindlemoor")); // he is with the party now
        Assert.Contains(s.Hire("tobin"), m => m.Kind == MessageKind.Bad);
        Assert.Equal(6, s.State.Heroes.Count());
    }

    [Fact]
    public void Hiring_NeedsTheWage_AndRoomForAnother_AndTheRightInn()
    {
        var s = InTown("saltreach");
        s.State.Gold = 0;
        foreach (var c in s.State.Party)
        {
            c.Gold = 0;
        }
        Assert.Contains(s.Hire("garrick"), m => m.Kind == MessageKind.Bad);
        s.State.Gold = 10_000;
        Assert.Contains(s.Hire("tobin"), m => m.Kind == MessageKind.Bad); // he waits in Brindlemoor
        s.Hire("garrick");
        s.Hire("marisol");
        Assert.Equal(GameState.MaxHirelings, s.State.Hirelings.Count());
        s.State.MapId = "brindlemoor";
        Assert.Contains(s.Hire("tobin"), m => m.Text.Contains("no more than", StringComparison.Ordinal));
    }

    [Fact]
    public void Wages_ArePaidEachDay_AndUnpaidHirelingsLeave()
    {
        var s = InTown();
        s.Hire("tobin");
        var wage = s.WageOf(s.State.Party[^1]);
        var gold = s.State.Gold;
        NextDay(s);
        Assert.Equal(gold - wage, s.State.Gold);
        Assert.Equal(7, s.State.Party.Count);

        s.State.Gold = 0;
        foreach (var c in s.State.Party)
        {
            c.Gold = 0;
        }
        s.State.Party[^1].Backpack.Add(new ItemInstance("potion_healing"));
        NextDay(s);
        Assert.Equal(6, s.State.Party.Count);
        Assert.Contains(s.State.Party, c => c.Backpack.Any(i => i.ItemId == "potion_healing")); // he left his pack with the party
        Assert.Contains(s.HirelingsAt("brindlemoor"), h => h.Id == "tobin"); // and is back at the inn
    }

    [Fact]
    public void Hirelings_TakeNoExperience_AndDontTrain()
    {
        var s = InTown();
        s.Hire("tobin");
        var tobin = s.State.Party[^1];
        var xp = tobin.Experience;
        var hero = s.State.Party[0].Experience;
        s.AwardXp(100, []);
        Assert.Equal(hero + 100, s.State.Party[0].Experience);
        Assert.Equal(xp, tobin.Experience);

        tobin.Experience = Rulebook.XpForLevel(s.Content.Class(tobin.Class), tobin.Level + 1);
        var training = s.Content.Map("brindlemoor").AllEvents.First(e => e.Type == MapEventKind.Training);
        Assert.Contains(s.Town.Train(tobin, training), m => m.Text.Contains("paid to fight", StringComparison.Ordinal));
        Assert.Equal(2, tobin.Level);
    }

    [Fact]
    public void Hirelings_AreDismissed_NotLeftAtTheInn_AndDontTakeAHerosPlace()
    {
        var s = InTown();
        s.Hire("tobin");
        Assert.Contains(s.Town.LeaveAtInn(6), m => m.Text.Contains("dismiss", StringComparison.Ordinal));
        Assert.Empty(s.State.Roster);

        // A hero who stays behind can rejoin: hirelings don't count against the six.
        s.Town.LeaveAtInn(0);
        Assert.Equal(6, s.State.Party.Count);
        s.Town.JoinParty(0);
        Assert.Equal(7, s.State.Party.Count);

        var tobin = s.State.Party.Single(c => c.Hireling is not null);
        s.Dismiss(tobin);
        Assert.Equal(6, s.State.Party.Count);
        Assert.All(s.State.Party, c => Assert.Null(c.Hireling));
    }

    [Fact]
    public void Hirelings_AreSaved()
    {
        var s = InTown();
        s.Hire("tobin");
        var loaded = SaveGameService.Deserialize(SaveGameService.Serialize(new SaveFile { State = s.State })).State;
        Assert.Equal("tobin", loaded.Party[^1].Hireling);
        Assert.Equal(s.State.WagesPaidDay, loaded.WagesPaidDay);
    }

    [Fact]
    public void NoHirelings_InTheDailyChallenge()
    {
        var s = InTown();
        s.State.DailyChallenge = "2026-10-10";
        Assert.Contains(s.Hire("tobin"), m => m.Kind == MessageKind.Bad);
    }

    [Fact]
    public void AnEightStrongParty_FightsABattle_AndOnlyTheHeroesGainExperience()
    {
        var s = InTown("wintermere");
        s.State.Gold = 10_000;
        s.Hire("ragna");
        s.Hire("aldwyn");
        Assert.Equal(8, s.State.Party.Count);
        var heroXp = s.State.Party[0].Experience;
        var hiredXp = s.State.Party[7].Experience;
        s.StartCombat(AVAMMB1.Core.Combat.CombatEngine.Spawn(s.Content.Monster("ogre"), 4, s.Random).ToList(), new StepResult());
        var combat = s.Combat!;
        combat.Advance();
        var acted = new HashSet<AVAMMB1.Core.Characters.Character>();
        for (var guard = 0; combat.Outcome == AVAMMB1.Core.Combat.CombatOutcome.Ongoing && guard < 2000; guard++)
        {
            acted.Add(combat.ActiveCharacter!);
            combat.Act(AVAMMB1.Core.Combat.AutoTactics.Choose(s.Rules, s.Spells, combat, combat.ActiveCharacter!, s.State.Party, offensiveSpells: true));
        }
        Assert.Equal(AVAMMB1.Core.Combat.CombatOutcome.Victory, s.EndCombat().Outcome);
        Assert.Contains(s.State.Party[7], acted); // the eighth fights too
        Assert.True(s.State.Party[0].Experience > heroXp);
        Assert.Equal(hiredXp, s.State.Party[7].Experience);
    }
}
