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
    public int MaxHp { get; } = Math.Max(1, hp);
    /// <summary>Conditions (only Asleep and Paralyzed are used for monsters).</summary>
    public Condition Conditions { get; set; }
    /// <summary>Whether the monster ran away.</summary>
    public bool Fled { get; set; }
    /// <summary>Armor class lost to weakening magic during this battle.</summary>
    public int ArmorPenalty { get; set; }

    /// <summary>Current armor class including penalties.</summary>
    public int ArmorClass => Def.ArmorClass - ArmorPenalty;

    /// <summary>Display label, e.g. "Goblin #2".</summary>
    public string Label { get; set; } = def.Name;

    /// <summary>Still in the fight.</summary>
    public bool IsActive => Hp > 0 && !Fled;

    /// <summary>Killed (as opposed to fled).</summary>
    public bool IsDead => Hp <= 0;

    /// <summary>Can act this turn.</summary>
    public bool CanAct => IsActive && (Conditions & (Condition.Asleep | Condition.Paralyzed)) == 0;
}
