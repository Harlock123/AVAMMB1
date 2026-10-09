using AVAMMB1.Core.Combat;
using AVAMMB1.Core.Items;
using AVAMMB1.Core.Persistence;
using AVAMMB1.Core.Rules;
using AVAMMB1.Core.Session;

namespace AVAMMB1.Tests;

/// <summary>Spell levels, tomes and the newer spell effects.</summary>
public class SpellTests
{
    private static void SetLevel(GameSession s, AVAMMB1.Core.Characters.Character c, int level)
    {
        c.Experience = Rulebook.XpForLevel(s.Content.Class(c.Class), level);
        while (s.Rules.LevelUp(c, s.Random) is not null)
        {
        }
    }

    [Fact]
    public void Content_HasSixSpellLevelsForBothSchools()
    {
        var spells = TestContent.Content.Spells.Values.Where(s => s.Learnable).ToList();
        Assert.True(spells.Count >= 40);
        foreach (var school in Enum.GetValues<SpellSchool>())
        {
            for (var level = 1; level <= Rulebook.TopSpellLevel; level++)
            {
                Assert.Contains(spells, s => s.School == school && s.Level == level && !s.Tome);
            }
        }
    }

    [Fact]
    public void SpellLevelSix_ArrivesAtCasterLevelEleven()
    {
        var s = TestContent.NewSession();
        var sorc = TestContent.Make(s, "sorcerer", stats: 16);
        SetLevel(s, sorc, 10);
        Assert.Equal(5, s.Rules.MaxSpellLevel(sorc));
        SetLevel(s, sorc, 11);
        Assert.Equal(6, s.Rules.MaxSpellLevel(sorc));
        Assert.Contains(s.Rules.KnownSpells(sorc), sp => sp.Id == "s_prismatic");
    }

    [Fact]
    public void TomeSpells_AreNotLearnedByLevelling_OnlyByReading()
    {
        var s = TestContent.StartedSession();
        var sorc = s.State.Party.Single(c => c.Class == "sorcerer");
        var cleric = s.State.Party.Single(c => c.Class == "cleric");
        SetLevel(s, sorc, 9);
        Assert.DoesNotContain(s.Rules.KnownSpells(sorc), sp => sp.Id == "s_chain");

        sorc.Backpack.Add(new ItemInstance("tome_chain"));
        cleric.Backpack.Add(new ItemInstance("tome_chain"));
        Assert.False(s.Spells.UseItem(cleric, cleric.Backpack.Count - 1, s.State, null, -1, null).Success); // wrong school
        var r = s.Spells.UseItem(sorc, sorc.Backpack.Count - 1, s.State, null, -1, null);
        Assert.True(r.Success, string.Join(" ", r.Messages.Select(m => m.Text)));
        Assert.Contains(s.Rules.KnownSpells(sorc), sp => sp.Id == "s_chain");
        Assert.DoesNotContain(sorc.Backpack, i => i.ItemId == "tome_chain"); // consumed

        // Persisted with the character.
        var loaded = SaveGameService.Deserialize(SaveGameService.Serialize(new SaveFile { State = s.State })).State;
        Assert.Contains("s_chain", loaded.Party.Single(c => c.Class == "sorcerer").LearnedSpells);
    }

    [Fact]
    public void Tome_TooAdvancedForTheReader_IsKept()
    {
        var s = TestContent.StartedSession();
        var sorc = s.State.Party.Single(c => c.Class == "sorcerer"); // level 1
        sorc.Backpack.Add(new ItemInstance("tome_iron_grip"));
        var r = s.Spells.UseItem(sorc, sorc.Backpack.Count - 1, s.State, null, -1, null);
        Assert.False(r.Success);
        Assert.Contains(r.Messages, m => m.Text.Contains("beyond", StringComparison.Ordinal));
        Assert.Contains(sorc.Backpack, i => i.ItemId == "tome_iron_grip");
    }

    [Fact]
    public void WeakenArmor_LowersEnemyArmorForTheBattle()
    {
        var s = TestContent.StartedSession(2);
        var sorc = s.State.Party.Single(c => c.Class == "sorcerer");
        SetLevel(s, sorc, 3);
        var ogre = new MonsterInstance(s.Content.Monster("ogre"), 80);
        var engine = new CombatEngine(s.Rules, s.Random, s.State, [ogre]);
        var before = ogre.ArmorClass;
        Assert.True(s.Spells.Cast(sorc, s.Content.Spell("s_weaken"), s.State, engine, -1, ogre).Success);
        Assert.Equal(before - 3, ogre.ArmorClass);
    }

    [Fact]
    public void StoneToFlesh_CuresAPetrifiedAlly()
    {
        var s = TestContent.StartedSession();
        var cleric = s.State.Party.Single(c => c.Class == "cleric");
        SetLevel(s, cleric, 9);
        cleric.LearnedSpells.Add("c_stone_flesh");
        var victim = s.State.Party[0];
        victim.Conditions |= Condition.Stoned;
        Assert.False(victim.IsAlive);
        var r = s.Spells.Cast(cleric, s.Content.Spell("c_stone_flesh"), s.State, null, 0, null);
        Assert.True(r.Success, string.Join(" ", r.Messages.Select(m => m.Text)));
        Assert.False(victim.Has(Condition.Stoned));
        Assert.True(victim.IsAlive);
    }

    [Fact]
    public void Tomes_AreFindableInTheWorld()
    {
        var db = TestContent.Content;
        foreach (var tome in db.Items.Values.Where(i => i.Kind == ItemKind.Tome))
        {
            var inShop = db.Shops.Values.Any(sh => sh.Stock.Contains(tome.Id));
            var inMap = db.Maps.Values.SelectMany(m => m.AllEvents).Any(e => e.Items.Contains(tome.Id));
            Assert.True(inShop || inMap, $"{tome.Id} can't be found anywhere");
            Assert.True(db.Spells[tome.TeachSpell!].Tome);
        }
    }
}
