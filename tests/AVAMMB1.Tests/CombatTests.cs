using AVAMMB1.Core.Combat;
using AVAMMB1.Core.Dice;
using AVAMMB1.Core.Rules;
using AVAMMB1.Core.Session;

namespace AVAMMB1.Tests;

public class CombatTests
{
    [Theory]
    [InlineData(20, -10, 30, true)]  // natural 20 always hits
    [InlineData(1, 50, 0, false)]    // natural 1 always misses
    [InlineData(10, 2, 4, true)]     // 12 >= 12
    [InlineData(9, 2, 4, false)]     // 11 < 12
    public void ToHit_Rule(int roll, int bonus, int ac, bool expected) =>
        Assert.Equal(expected, Rulebook.IsHit(roll, bonus, ac));

    [Fact]
    public void MeleeDamage_AddsMightBonus_MinimumOne()
    {
        var s = TestContent.NewSession();
        var c = TestContent.Make(s, "knight", stats: 18); // Might 18 => +2, long sword 1d8
        Assert.Equal(1 + 2, s.Rules.RollMeleeDamage(c, new ScriptedRandom(1)));
        Assert.Equal(8 + 2, s.Rules.RollMeleeDamage(c, new ScriptedRandom(8)));
        var weakling = TestContent.Make(s, "robber", stats: 3);
        Assert.Equal(1, s.Rules.RollMeleeDamage(weakling, new ScriptedRandom(1)));
    }

    [Fact]
    public void AttackBonus_ScalesWithLevelAndBlindness()
    {
        var s = TestContent.NewSession();
        var c = TestContent.Make(s, "knight", stats: 16);
        var b1 = s.Rules.MeleeAttackBonus(c);
        Assert.Equal(1 + 2, b1); // level 1 * 1.0 + accuracy bonus
        c.Level = 5;
        Assert.Equal(5 + 2, s.Rules.MeleeAttackBonus(c));
        Assert.Equal(2, s.Rules.AttacksPerRound(c));
        c.Conditions |= Condition.Blinded;
        Assert.Equal(5 + 2 - 4, s.Rules.MeleeAttackBonus(c));
    }

    [Fact]
    public void MonsterResistances_AndHolyVsUndead()
    {
        var s = TestContent.StartedSession();
        var slime = new MonsterInstance(s.Content.Monster("green_slime"), 100);
        var bones = new MonsterInstance(s.Content.Monster("rattlebones"), 100);
        var engine = new CombatEngine(s.Rules, s.Random, s.State, [slime, bones]);
        var log = new List<GameMessage>();
        Assert.Equal(0, engine.DamageMonster(slime, 20, Element.Acid, log));
        Assert.Equal(15, engine.DamageMonster(slime, 20, Element.Physical, log));
        Assert.Equal(40, engine.DamageMonster(bones, 20, Element.Holy, log));
    }

    [Fact]
    public void Battle_RunsToCompletion_WithAutoAttacks()
    {
        for (var seed = 0; seed < 25; seed++)
        {
            var s = TestContent.StartedSession(seed);
            var monsters = CombatEngine.Spawn(s.Content.Monster("goblin"), 3, s.Random);
            var start = new StepResult();
            s.StartCombat(monsters, start);
            var combat = s.Combat!;
            combat.Advance();
            var guard = 0;
            while (combat.Outcome == CombatOutcome.Ongoing && guard++ < 500)
            {
                var c = combat.ActiveCharacter!;
                var action = combat.IsInFrontRank(c)
                    ? new CombatAction(CombatActionKind.Attack)
                    : s.Rules.HasMissileWeapon(c) ? new CombatAction(CombatActionKind.Shoot) : new CombatAction(CombatActionKind.Block);
                combat.Act(action);
            }
            Assert.NotEqual(CombatOutcome.Ongoing, combat.Outcome);
            var xpBefore = s.State.Party[0].Experience;
            var (outcome, _) = s.EndCombat();
            Assert.Null(s.Combat);
            if (outcome == CombatOutcome.Victory && s.State.Party[0].IsAlive && !s.State.Party[0].Has(Condition.Unconscious))
            {
                Assert.Equal(xpBefore + 36, s.State.Party[0].Experience);
            }
        }
    }

    [Fact]
    public void BackRank_CannotMelee()
    {
        var s = TestContent.StartedSession(3);
        var monsters = CombatEngine.Spawn(s.Content.Monster("ogre"), 1, s.Random);
        var start = new StepResult();
        s.StartCombat(monsters, start);
        var combat = s.Combat!;
        Assert.True(combat.IsInFrontRank(s.State.Party[0]));
        Assert.False(combat.IsInFrontRank(s.State.Party[5]));
        // When a front-liner falls, the next member steps up.
        s.Rules.ApplyDamage(s.State.Party[0], 1000);
        Assert.True(combat.IsInFrontRank(s.State.Party[3]));
    }

    [Fact]
    public void SleepSpell_DoesNotAffectUndead()
    {
        var s = TestContent.StartedSession(5);
        var bones = new MonsterInstance(s.Content.Monster("rattlebones"), 10);
        var engine = new CombatEngine(s.Rules, new ScriptedRandom(0), s.State, [bones]);
        var log = new List<GameMessage>();
        Assert.False(engine.InflictMonster(bones, Condition.Asleep, 10, log));
        var goblin = new MonsterInstance(s.Content.Monster("goblin"), 10);
        var engine2 = new CombatEngine(s.Rules, new ScriptedRandom(0), s.State, [goblin]);
        Assert.True(engine2.InflictMonster(goblin, Condition.Asleep, 10, log));
        Assert.False(goblin.CanAct);
    }

    [Fact]
    public void CastingSpell_ConsumesSpellPoints_AndDamages()
    {
        var s = TestContent.StartedSession(9);
        var ysolde = s.State.Party.Single(c => c.Class == "sorcerer");
        var ogre = new MonsterInstance(s.Content.Monster("ogre"), 60);
        var engine = new CombatEngine(s.Rules, new DefaultRandomSource(1), s.State, [ogre]);
        var sp = ysolde.Sp;
        var result = s.Spells.Cast(ysolde, s.Content.Spell("s_spark"), s.State, engine, -1, ogre);
        Assert.True(result.Success);
        Assert.Equal(sp - 1, ysolde.Sp);
        Assert.True(ogre.Hp < 60);
        ysolde.Sp = 0;
        Assert.False(s.Spells.Cast(ysolde, s.Content.Spell("s_spark"), s.State, engine, -1, ogre).Success);
    }

    [Fact]
    public void Defeat_WhenEveryoneDown()
    {
        var s = TestContent.StartedSession(11);
        foreach (var c in s.State.Party)
        {
            s.Rules.ApplyDamage(c, c.Hp);
        }
        var monsters = CombatEngine.Spawn(s.Content.Monster("goblin"), 1, s.Random);
        var start = new StepResult();
        s.StartCombat(monsters, start);
        s.Combat!.Advance();
        Assert.Equal(CombatOutcome.Defeat, s.Combat.Outcome);
    }

    [Fact]
    public void Bribe_PaysGold()
    {
        var s = TestContent.StartedSession(1);
        var monsters = CombatEngine.Spawn(s.Content.Monster("bandit"), 2, s.Random);
        var start = new StepResult();
        s.StartCombat(monsters, start);
        var cost = s.Combat!.BribeCost;
        Assert.NotNull(cost);
        var gold = s.State.Gold;
        var engine = new CombatEngine(s.Rules, new ScriptedRandom(0), s.State, s.Combat.Monsters);
        engine.TryBribe();
        Assert.Equal(CombatOutcome.Bribed, engine.Outcome);
        Assert.Equal(gold - cost, s.State.Gold);
        var undead = new CombatEngine(s.Rules, s.Random, s.State, CombatEngine.Spawn(s.Content.Monster("zombie"), 1, s.Random));
        Assert.Null(undead.BribeCost);
    }
}
