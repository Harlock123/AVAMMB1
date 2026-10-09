using AVAMMB1.Core.Characters;
using AVAMMB1.Core.Magic;
using AVAMMB1.Core.Rules;

namespace AVAMMB1.Core.Combat;

/// <summary>
/// Simple, predictable battle tactics used by the Auto-fight command and the balance simulator:
/// heal badly wounded allies (spells first, then healing potions), optionally blast with spells from
/// the back rank, otherwise attack from the front, shoot from the back, or block.
/// </summary>
public static class AutoTactics
{
    /// <summary>Picks an action for the active character.</summary>
    /// <param name="rules">Rulebook.</param>
    /// <param name="spells">Spell caster (for castability checks).</param>
    /// <param name="combat">The battle.</param>
    /// <param name="c">Character whose turn it is.</param>
    /// <param name="party">The party (ally indexes refer to it).</param>
    /// <param name="offensiveSpells">Whether attack spells may be cast (they cost spell points).</param>
    public static CombatAction Choose(Rulebook rules, SpellCaster spells, CombatEngine combat, Character c, IReadOnlyList<Character> party, bool offensiveSpells)
    {
        var allies = party.ToList();
        var hurt = allies.Where(p => p.IsAlive && p.Hp < p.MaxHp / 2).OrderBy(p => p.Hp).FirstOrDefault();
        var known = rules.KnownSpells(c).Where(sp => sp.Combat && spells.CanCast(c, sp, inCombat: true) is null).ToList();
        var heal = known.Where(sp => sp.Effect == EffectKind.Heal && sp.Target == TargetKind.Ally).OrderByDescending(sp => sp.Level).FirstOrDefault();
        if (hurt is not null && heal is not null)
        {
            return new CombatAction(CombatActionKind.Cast, Ally: allies.IndexOf(hurt), SpellId: heal.Id);
        }
        if (hurt is not null && rules.HasAbility(c, Content.ClassAbility.LayOnHands) && !combat.HasLaidHands(c))
        {
            return new CombatAction(CombatActionKind.LayOnHands, Ally: allies.IndexOf(hurt));
        }
        var potion = c.Backpack.FindIndex(i => i.ItemId == "potion_healing");
        if (hurt is not null && hurt.Hp * 4 < hurt.MaxHp && potion >= 0)
        {
            return new CombatAction(CombatActionKind.UseItem, Ally: allies.IndexOf(hurt), ItemIndex: potion);
        }
        var active = combat.Monsters.Where(m => m.IsActive).ToList();
        if (offensiveSpells && active.Count > 0 && !combat.IsInFrontRank(c))
        {
            var nukes = known.Where(sp => sp.Effect == EffectKind.Damage && (!sp.UndeadOnly || active.Any(m => m.Def.Undead))).ToList();
            var nuke = active.Count >= 3
                ? nukes.OrderByDescending(sp => sp.Target != TargetKind.Enemy).ThenByDescending(sp => sp.Level).FirstOrDefault()
                : nukes.Where(sp => sp.Target == TargetKind.Enemy).OrderByDescending(sp => sp.Level).FirstOrDefault() ?? nukes.OrderByDescending(sp => sp.Level).FirstOrDefault();
            // Casters keep some spell points for healing unless the fight is big.
            var reserve = heal is not null && active.Count < 3 ? c.MaxSp / 3 : 0;
            if (nuke is not null && c.Sp - nuke.Cost >= reserve)
            {
                var target = active.OrderBy(m => m.Hp).First();
                return new CombatAction(CombatActionKind.Cast, Target: combat.Monsters.IndexOf(target), SpellId: nuke.Id);
            }
        }
        if (!combat.IsInFrontRank(c) && combat.CanAim(c))
        {
            return new CombatAction(CombatActionKind.AimedShot);
        }
        return combat.IsInFrontRank(c) ? new CombatAction(CombatActionKind.Attack)
            : rules.HasMissileWeapon(c) ? new CombatAction(CombatActionKind.Shoot)
            : new CombatAction(CombatActionKind.Block);
    }
}
