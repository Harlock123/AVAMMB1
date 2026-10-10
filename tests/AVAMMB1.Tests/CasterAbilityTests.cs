using AVAMMB1.Core.Combat;
using AVAMMB1.Core.Rules;
using AVAMMB1.Core.Session;

namespace AVAMMB1.Tests;

/// <summary>Clerics turn undead; sorcerers overcharge spells.</summary>
public class CasterAbilityTests
{
    private static (GameSession S, CombatEngine Combat) Battle(string cls, int level, params (string Monster, int Count)[] foes) => Battle(5, cls, level, foes);

    private static (GameSession S, CombatEngine Combat) Battle(int seed, string cls, int level, params (string Monster, int Count)[] foes)
    {
        var s = TestContent.StartedSession(seed);
        var c = s.State.Party.First(p => p.Class == cls);
        c.Experience = Rulebook.XpForLevel(s.Content.Class(c.Class), level);
        while (s.Rules.LevelUp(c, s.Random) is not null) { }
        c.Sp = c.MaxSp;
        s.StartCombat(foes.SelectMany(f => CombatEngine.Spawn(s.Content.Monster(f.Monster), f.Count, s.Random)), new StepResult());
        var combat = s.Combat!;
        combat.Advance();
        while (combat.ActiveCharacter is { } a && a.Class != cls && combat.Outcome == CombatOutcome.Ongoing)
        {
            combat.Act(new CombatAction(CombatActionKind.Block));
        }
        return (s, combat);
    }

    [Fact]
    public void TurnUndead_DestroysWeakUndead_OncePerBattle()
    {
        var (_, combat) = Battle("cleric", 12, ("zombie", 3));
        var cleric = combat.ActiveCharacter!;
        Assert.True(combat.CanTurnUndead(cleric));
        var log = combat.Act(new CombatAction(CombatActionKind.TurnUndead));
        Assert.Contains(log, m => m.Text.Contains("crumbles to dust", StringComparison.Ordinal));
        Assert.All(combat.Monsters, m => Assert.False(m.IsActive));
    }

    [Fact]
    public void TurnUndead_NeedsUndead_AndSparesUniqueOnes()
    {
        var (_, living) = Battle("cleric", 12, ("kobold", 2));
        Assert.False(living.CanTurnUndead(living.ActiveCharacter!));
        Assert.DoesNotContain("turnUndead", TestContent.Content.Classes["knight"].Abilities);

        var (_, boss) = Battle("cleric", 20, ("wight_king", 1));
        var log = boss.Act(new CombatAction(CombatActionKind.TurnUndead));
        Assert.Contains(log, m => m.Text.Contains("laughs", StringComparison.Ordinal));
        Assert.True(boss.Monsters[0].IsActive);
    }

    [Fact]
    public void Overcharge_HitsHarder_ForDoubleTheCost()
    {
        int Damage(int seed, bool overcharge, out int spent)
        {
            var (s, combat) = Battle(seed, "sorcerer", 9, ("frost_giant", 1));
            var sorcerer = combat.ActiveCharacter!;
            var spell = s.Rules.KnownSpells(sorcerer).Where(sp => sp.Combat && sp.Effect == EffectKind.Damage && sp.Target == TargetKind.Enemy && sp.PerLevel > 0)
                .OrderByDescending(sp => sp.Level).First();
            var giant = combat.Monsters[0];
            var (hp, sp) = (giant.Hp, sorcerer.Sp);
            combat.Act(new CombatAction(CombatActionKind.Cast, 0, SpellId: spell.Id, Overcharge: overcharge));
            spent = sp - sorcerer.Sp;
            Assert.Equal(overcharge ? spell.Cost * 2 : spell.Cost, spent);
            return hp - giant.Hp;
        }
        var normal = Enumerable.Range(1, 10).Sum(seed => Damage(seed, false, out _));
        var charged = Enumerable.Range(1, 10).Sum(seed => Damage(seed, true, out _));
        Assert.True(charged > normal, $"overcharged {charged} vs {normal}");
    }
}
