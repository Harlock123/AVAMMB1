using AVAMMB1.Core.Content;
using AVAMMB1.Core.Rules;

namespace AVAMMB1.Core.Combat;

/// <summary>A live monster in combat.</summary>
/// <param name="def">Template.</param>
/// <param name="hp">Rolled hit points.</param>
public sealed class MonsterInstance(MonsterDef def, int hp)
{
    /// <summary>Template.</summary>
    public MonsterDef Def { get; } = def;
    /// <summary>Current hit points.</summary>
    public int Hp { get; set; } = Math.Max(1, hp);
    /// <summary>Maximum hit points.</summary>
    public int MaxHp { get; private set; } = Math.Max(1, hp);
    /// <summary>Conditions (only Asleep and Paralyzed are used for monsters).</summary>
    public Condition Conditions { get; set; }
    /// <summary>Whether the monster ran away.</summary>
    public bool Fled { get; set; }

    /// <summary>Driven off by a cleric's Turn Undead: it counts as defeated (experience and gold) though it fled.</summary>
    public bool Turned { get; set; }
    /// <summary>Armor class lost to weakening magic during this battle.</summary>
    public int ArmorPenalty { get; set; }

    /// <summary>
    /// A rare, tougher specimen: double hit points, +2 armor class, half again as much damage, and
    /// triple experience and gold with a chance of extra loot.
    /// </summary>
    public bool Elite { get; private set; }

    /// <summary>Current armor class including penalties.</summary>
    public int ArmorClass => Def.ArmorClass + (Elite ? 2 : 0) - ArmorPenalty;

    /// <summary>Turns this monster into an elite (see <see cref="Elite"/>).</summary>
    public void MakeElite()
    {
        if (Elite)
        {
            return;
        }
        Elite = true;
        MaxHp *= 2;
        Hp = MaxHp;
    }

    /// <summary>Damage after the elite bonus.</summary>
    /// <param name="damage">Rolled damage.</param>
    public int ScaleDamage(int damage)
    {
        var d = Elite ? damage * 3 / 2 : damage;
        return d <= 0 || DamagePercent == 100 ? d : Math.Max(1, d * DamagePercent / 100);
    }

    /// <summary>Damage in percent of normal (difficulty).</summary>
    public int DamagePercent { get; set; } = 100;

    /// <summary>Scales hit points (difficulty); call before the battle starts.</summary>
    /// <param name="percent">Percent of normal.</param>
    public void ScaleHp(int percent)
    {
        var full = Hp >= MaxHp;
        MaxHp = Math.Max(1, MaxHp * percent / 100);
        Hp = full ? MaxHp : Math.Clamp(Hp * percent / 100, 1, MaxHp);
    }

    /// <summary>Display label, e.g. "Goblin #2".</summary>
    public string Label { get; set; } = def.Name;

    /// <summary>Still in the fight.</summary>
    public bool IsActive => Hp > 0 && !Fled;

    /// <summary>Killed (as opposed to fled).</summary>
    public bool IsDead => Hp <= 0;

    /// <summary>Can act this turn.</summary>
    public bool CanAct => IsActive && (Conditions & (Condition.Asleep | Condition.Paralyzed)) == 0;
}
