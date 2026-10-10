using AVAMMB1.Core.Combat;
using AVAMMB1.Core.Items;
using AVAMMB1.Core.Persistence;
using AVAMMB1.Core.Rules;
using AVAMMB1.Core.Session;

namespace AVAMMB1.Tests;

/// <summary>New Game+: the same party, a stronger world.</summary>
public class NewGamePlusTests
{
    private static GameSession Won()
    {
        var s = TestContent.StartedSession();
        foreach (var c in s.State.Party)
        {
            c.Experience = Rulebook.XpForLevel(s.Content.Class(c.Class), 16);
            while (s.Rules.LevelUp(c, s.Random) is not null)
            {
            }
        }
        s.State.Won = true;
        s.State.Gold = 5000;
        s.State.Flags.UnionWith(["pell_met", "sun_king_slain"]);
        s.State.CompletedEvents.Add("cellars:3,4");
        s.State.Explored["cellars"] = "1";
        (s.State.MapId, s.State.X, s.State.Y) = ("vault", 3, 3);
        s.State.Party[0].Backpack.Add(new ItemInstance("vault_sigil"));
        s.State.Party[0].Backpack.Add(new ItemInstance("potion_healing"));
        s.State.Party[1].LearnedSpells.Add("s_chain");
        return s;
    }

    private static CombatOutcome Fight(GameSession s, IEnumerable<MonsterInstance> monsters)
    {
        s.StartCombat(monsters, new StepResult());
        var combat = s.Combat!;
        combat.Advance();
        for (var guard = 0; combat.Outcome == CombatOutcome.Ongoing && guard < 3000; guard++)
        {
            combat.Act(AutoTactics.Choose(s.Rules, s.Spells, combat, combat.ActiveCharacter!, s.State.Party, offensiveSpells: true));
        }
        return s.EndCombat().Outcome;
    }

    [Fact]
    public void OnlyAWinningParty_CanBeginNewGamePlus()
    {
        var s = TestContent.StartedSession();
        Assert.False(s.CanStartNewGamePlus);
        Assert.Empty(s.StartNewGamePlus());
        Assert.Equal(0, s.State.Cycle);
    }

    [Fact]
    public void TheWorldStartsAgain_ButThePartyKeepsWhatItEarned()
    {
        var s = Won();
        var levels = s.State.Party.Select(c => c.Level).ToList();
        var log = s.StartNewGamePlus();
        Assert.Contains(log, m => m.Text.StartsWith("New Game+ 1", StringComparison.Ordinal));
        Assert.Equal((1, 16), (s.State.Cycle, s.State.CyclePartyLevel));
        Assert.False(s.State.Won);
        Assert.Empty(s.State.Flags);
        Assert.Empty(s.State.CompletedEvents);
        Assert.False(s.State.Explored.ContainsKey("cellars"));
        var cfg = s.Content.Config;
        Assert.Equal((cfg.StartMap, cfg.StartX, cfg.StartY), (s.State.MapId, s.State.X, s.State.Y));

        Assert.Equal(levels, s.State.Party.Select(c => c.Level));
        Assert.Equal(5000, s.State.Gold);
        Assert.Contains("s_chain", s.State.Party[1].LearnedSpells);
        Assert.Contains(s.State.Party[0].Backpack, i => i.ItemId == "potion_healing");
        Assert.DoesNotContain(s.State.Party[0].Backpack, i => i.ItemId == "vault_sigil"); // quest items stay behind
        Assert.Empty(QuestJournal.Quests(s.State, s.Content)); // the quests begin again

        var loaded = SaveGameService.Deserialize(SaveGameService.Serialize(new SaveFile { State = s.State })).State;
        Assert.Equal((1, 16), (loaded.Cycle, loaded.CyclePartyLevel));
    }

    [Fact]
    public void Monsters_FightAsIfManyLevelsHigher()
    {
        var s = Won();
        s.StartNewGamePlus();
        var rat = s.Content.Monster("cellar_rat");
        s.StartCombat(CombatEngine.Spawn(rat, 1, s.Random), new StepResult());
        var m = s.Combat!.Monsters[0];
        Assert.Equal(16, m.LevelBoost);
        Assert.Equal(rat.Level + 16, m.Level);
        Assert.True(m.MaxHp >= rat.HitPoints.Min * NewGamePlus.HpPercent(rat.Level, 16) / 100);
        Assert.True(m.DamagePercent > 100 && m.RewardPercent > 100);
        Assert.True(m.ArmorClass > rat.ArmorClass);
        s.EndCombat();

        Assert.Equal(19, NewGamePlus.LevelBoost(2, 16)); // each cycle adds more
        Assert.Equal(0, NewGamePlus.LevelBoost(0, 16));
    }

    [Fact]
    public void Bosses_LeaveAscendantTreasures_OnlyInNewGamePlus()
    {
        int Drops(int cycle)
        {
            var s = Won();
            if (cycle > 0)
            {
                s.StartNewGamePlus();
            }
            var found = 0;
            for (var i = 0; i < 12; i++)
            {
                var boss = CombatEngine.Spawn(s.Content.Monster("kobold_chief"), 1, s.Random).ToList();
                boss[0].Hp = 1; // the treasure is what is being tested, not the fight
                var before = s.State.Party.Sum(c => c.Backpack.Count(b => NewGamePlus.AscendantItems.Contains(b.ItemId)));
                Assert.Equal(CombatOutcome.Victory, Fight(s, boss));
                found += s.State.Party.Sum(c => c.Backpack.Count(b => NewGamePlus.AscendantItems.Contains(b.ItemId))) - before;
                foreach (var c in s.State.Party)
                {
                    c.Backpack.RemoveAll(b => NewGamePlus.AscendantItems.Contains(b.ItemId)); // keep room in the packs
                    c.Hp = c.MaxHp;
                    c.Conditions = Condition.None;
                }
            }
            return found;
        }
        Assert.Equal(0, Drops(0));
        Assert.InRange(Drops(1), 3, 12);
    }

    [Fact]
    public void AscendantTreasures_AreTheBestOfTheirKind()
    {
        var db = TestContent.Content;
        foreach (var id in NewGamePlus.AscendantItems)
        {
            var item = db.Item(id);
            Assert.DoesNotContain(db.Shops.Values, sh => sh.Stock.Contains(id)); // never sold
            var others = db.Items.Values.Where(i => i.Kind == item.Kind && !NewGamePlus.AscendantItems.Contains(i.Id)).ToList();
            Assert.True(item.Price > others.Max(i => i.Price), $"{id} should be worth more than any other {item.Kind}");
        }
    }
}
