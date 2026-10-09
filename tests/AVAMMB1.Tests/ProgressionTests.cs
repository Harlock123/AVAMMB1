using AVAMMB1.Core.Characters;
using AVAMMB1.Core.Rules;

namespace AVAMMB1.Tests;

public class ProgressionTests
{
    [Theory]
    [InlineData(3, -3)]
    [InlineData(5, -2)]
    [InlineData(8, -1)]
    [InlineData(10, 0)]
    [InlineData(13, 1)]
    [InlineData(18, 2)]
    [InlineData(21, 3)]
    [InlineData(25, 4)]
    public void StatBonus_Table(int value, int expected) => Assert.Equal(expected, Rulebook.StatBonus(value));

    [Fact]
    public void XpTable_DoublesThenGrowsLinearly()
    {
        var knight = TestContent.Content.Class("knight");
        Assert.Equal(0, Rulebook.XpForLevel(knight, 1));
        Assert.Equal(250, Rulebook.XpForLevel(knight, 2));
        Assert.Equal(750, Rulebook.XpForLevel(knight, 3));
        Assert.Equal(1750, Rulebook.XpForLevel(knight, 4));
        var l12 = Rulebook.XpForLevel(knight, 12);
        Assert.Equal(l12 + 250 * 1024, Rulebook.XpForLevel(knight, 13));
        for (var l = 2; l < 30; l++)
        {
            Assert.True(Rulebook.XpForLevel(knight, l + 1) > Rulebook.XpForLevel(knight, l));
        }
    }

    [Fact]
    public void NewCharacter_HasClassHitPointsAndEquipment()
    {
        var s = TestContent.NewSession();
        var c = TestContent.Make(s, "knight", stats: 16);
        Assert.Equal(12 + 2, c.MaxHp); // d12 max + Endurance 16 bonus
        Assert.Equal(c.MaxHp, c.Hp);
        Assert.Equal(0, c.MaxSp);
        Assert.Equal("long_sword", c.Equipment[EquipSlot.Weapon].ItemId);
        Assert.Equal("ring_mail", c.Equipment[EquipSlot.Armor].ItemId);
        Assert.Empty(c.Backpack);
        // ring mail 3 + small shield 1 + speed 16 bonus 2
        Assert.Equal(6, s.Rules.ArmorClass(c));
    }

    [Fact]
    public void Requirements_AreEnforced()
    {
        var s = TestContent.NewSession();
        var weak = TestContent.Stats(10);
        Assert.Throws<ArgumentException>(() => s.Factory.Create("Weak", "human", "knight", Sex.Male, Alignment.Good, weak));
        Assert.Throws<ArgumentException>(() => s.Factory.Create("Bad", "human", "paladin", Sex.Male, Alignment.Evil, TestContent.Stats(16)));
        var eligible = s.Factory.EligibleClasses(weak, Alignment.Neutral).Select(c => c.Id).ToList();
        Assert.Equal(["robber"], eligible);
    }

    [Fact]
    public void RaceModifiers_AreAppliedAndClamped()
    {
        var elf = TestContent.Content.Race("elf");
        var raw = TestContent.Stats(3);
        var final = CharacterFactory.ApplyRace(raw, elf);
        Assert.Equal(5, final[Stat.Intellect]);
        Assert.Equal(3, final[Stat.Endurance]); // 3 - 2 clamped to 3
    }

    [Fact]
    public void Caster_GainsSpellPointsAndSpellLevels()
    {
        var s = TestContent.NewSession();
        var sorc = TestContent.Make(s, "sorcerer", stats: 16);
        Assert.Equal(1, s.Rules.CasterLevel(sorc));
        Assert.Equal(1 * (2 + 2) + 1, sorc.MaxSp);
        Assert.Equal(1, s.Rules.MaxSpellLevel(sorc));
        Assert.All(s.Rules.KnownSpells(sorc), sp => Assert.Equal(1, sp.Level));
        Assert.DoesNotContain(s.Rules.KnownSpells(sorc), sp => !sp.Learnable);

        var paladin = TestContent.Make(s, "paladin", stats: 16);
        Assert.Equal(0, paladin.MaxSp);
        Assert.Empty(s.Rules.KnownSpells(paladin));
    }

    [Fact]
    public void LevelUp_RequiresXpAndRaisesStats()
    {
        var s = TestContent.NewSession();
        var c = TestContent.Make(s, "cleric", stats: 16);
        Assert.Null(s.Rules.LevelUp(c, s.Random));
        c.Experience = Rulebook.XpForLevel(s.Content.Class("cleric"), 3);
        var hpBefore = c.MaxHp;
        var r1 = s.Rules.LevelUp(c, new ScriptedRandom(8));
        Assert.NotNull(r1);
        Assert.Equal(2, c.Level);
        Assert.Equal(hpBefore + 8 + 2, c.MaxHp);
        var r2 = s.Rules.LevelUp(c, new ScriptedRandom(1));
        Assert.Equal(3, r2!.NewLevel);
        Assert.Equal(2, r2.NewSpellLevel); // level 3 cleric unlocks spell level 2
        Assert.Equal(c.MaxSp, c.Sp);
        Assert.Null(s.Rules.LevelUp(c, s.Random));
    }

    [Fact]
    public void Damage_KnocksOutThenKills()
    {
        var s = TestContent.NewSession();
        var c = TestContent.Make(s, "knight", stats: 16);
        Assert.True(s.Rules.ApplyDamage(c, c.Hp));
        Assert.True(c.Has(Condition.Unconscious));
        Assert.True(c.IsAlive);
        Assert.False(c.CanAct);
        Assert.True(s.Rules.ApplyDamage(c, 100));
        Assert.True(c.Has(Condition.Dead));
        Assert.False(c.IsAlive);
        Assert.Equal(0, Rulebook.Heal(c, 10));
    }

    [Fact]
    public void Training_CostsGoldAndLevelsUp()
    {
        var s = TestContent.StartedSession();
        var c = s.State.Party[0];
        var ev = new AVAMMB1.Core.Content.MapEventDef { Type = AVAMMB1.Core.Content.MapEventKind.Training };
        c.Experience = 100000;
        var gold = s.State.Gold;
        var log = s.Town.Train(c, ev);
        Assert.Equal(2, c.Level);
        Assert.Equal(gold - 12, s.State.Gold);
        Assert.Contains(log, m => m.Text.Contains("level 2"));
        s.State.Gold = 0;
        s.Town.Train(c, ev);
        Assert.Equal(2, c.Level);
    }
}
