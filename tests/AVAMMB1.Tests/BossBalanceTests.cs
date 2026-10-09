using AVAMMB1.Core.Combat;
using AVAMMB1.Core.Rules;
using AVAMMB1.Core.Session;
using Xunit.Abstractions;

namespace AVAMMB1.Tests;

/// <summary>
/// Balance guard (deterministic seeds): each boss should be beatable by a party at its intended level
/// using simple tactics and starting equipment, but not trivial two levels earlier.
/// </summary>
public class BossBalanceTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("vault_warden", null, 0, 7, true)]
    [InlineData("vault_warden", null, 0, 5, false)]
    [InlineData("stone_wyrm", "rock_beetle", 2, 8, true)]
    [InlineData("stone_wyrm", "rock_beetle", 2, 6, false)]
    [InlineData("drowned_hydra", null, 0, 9, true)]
    [InlineData("drowned_hydra", null, 0, 7, false)]
    [InlineData("crypt_lich", "ghoul", 2, 5, true)]
    [InlineData("sphinx", null, 0, 10, true)]
    [InlineData("sphinx", null, 0, 8, false)]
    [InlineData("sun_king", "guardian_mummy", 2, 12, true)]
    [InlineData("sun_king", "guardian_mummy", 2, 9, false)]
    public void Boss_IsChallengingButFair(string boss, string? adds, int addCount, int level, bool shouldUsuallyWin)
    {
        var wins = 0;
        for (var seed = 0; seed < 20; seed++)
        {
            var s = TestContent.StartedSession(seed);
            foreach (var c in s.State.Party)
            {
                c.Experience = Rulebook.XpForLevel(s.Content.Class(c.Class), level);
                while (s.Rules.LevelUp(c, s.Random) is not null) { }
            }
            var monsters = CombatEngine.Spawn(s.Content.Monster(boss), 1, s.Random).ToList();
            if (adds is not null) monsters.AddRange(CombatEngine.Spawn(s.Content.Monster(adds), addCount, s.Random));
            s.StartCombat(monsters, new StepResult());
            var combat = s.Combat!;
            combat.Advance();
            var guard = 0;
            while (combat.Outcome == CombatOutcome.Ongoing && guard++ < 3000)
            {
                var c = combat.ActiveCharacter!;
                var hurt = s.State.Party.Where(p => p.IsAlive && p.Hp < p.MaxHp / 2).OrderBy(p => p.Hp).FirstOrDefault();
                CombatAction action;
                var known = s.Rules.KnownSpells(c).Where(sp => sp.Combat && sp.Cost <= c.Sp).ToList();
                var heal = known.Where(sp => sp.Effect == AVAMMB1.Core.Rules.EffectKind.Heal && sp.Target == AVAMMB1.Core.Rules.TargetKind.Ally).OrderByDescending(sp => sp.Level).FirstOrDefault();
                var nuke = known.Where(sp => sp.Effect == AVAMMB1.Core.Rules.EffectKind.Damage && !sp.UndeadOnly).OrderByDescending(sp => sp.Level).FirstOrDefault();
                if (hurt is not null && heal is not null)
                    action = new CombatAction(CombatActionKind.Cast, Ally: s.State.Party.IndexOf(hurt), SpellId: heal.Id);
                else if (nuke is not null && !combat.IsInFrontRank(c))
                    action = new CombatAction(CombatActionKind.Cast, Target: combat.Monsters.IndexOf(combat.Monsters.First(m => m.IsActive)), SpellId: nuke.Id);
                else
                    action = combat.IsInFrontRank(c) ? new CombatAction(CombatActionKind.Attack)
                        : s.Rules.HasMissileWeapon(c) ? new CombatAction(CombatActionKind.Shoot) : new CombatAction(CombatActionKind.Block);
                combat.Act(action);
            }
            if (combat.Outcome == CombatOutcome.Victory) wins++;
        }
        output.WriteLine($"{boss} vs level {level}: {wins}/20");
        if (shouldUsuallyWin)
        {
            Assert.True(wins >= 15, $"{boss} is too hard at level {level}: party won {wins}/20");
        }
        else
        {
            Assert.True(wins <= 5, $"{boss} is too easy at level {level}: party won {wins}/20");
        }
    }
}
