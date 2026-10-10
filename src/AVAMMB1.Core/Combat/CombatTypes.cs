using AVAMMB1.Core.Items;

namespace AVAMMB1.Core.Combat;

/// <summary>State of a battle.</summary>
public enum CombatOutcome
{
    /// <summary>Still fighting.</summary>
    Ongoing,
    /// <summary>All monsters were defeated or fled.</summary>
    Victory,
    /// <summary>The party was wiped out.</summary>
    Defeat,
    /// <summary>The party escaped.</summary>
    Fled,
    /// <summary>The monsters accepted a bribe.</summary>
    Bribed,
}

/// <summary>Kinds of action a party member can take in combat.</summary>
public enum CombatActionKind
{
    /// <summary>Melee attack (front rank only).</summary>
    Attack,
    /// <summary>Missile attack (requires a missile weapon).</summary>
    Shoot,
    /// <summary>Cast a spell.</summary>
    Cast,
    /// <summary>Use an item from the backpack.</summary>
    UseItem,
    /// <summary>Defend: armor bonus until next turn.</summary>
    Block,
    /// <summary>The whole party tries to run away.</summary>
    Run,
    /// <summary>Knights: protect an ally this round.</summary>
    Guard,
    /// <summary>Paladins: heal an ally once per battle.</summary>
    LayOnHands,
    /// <summary>Archers: one careful shot at +4 to hit for double damage.</summary>
    AimedShot,
    /// <summary>Clerics, once per battle: undead flee or crumble.</summary>
    TurnUndead,
}

/// <summary>An action chosen by the player for the active character.</summary>
/// <param name="Kind">Action kind.</param>
/// <param name="Target">Index into <see cref="CombatEngine.Monsters"/> for enemy targets (-1 = automatic).</param>
/// <param name="Ally">Party index for ally targets (-1 = none).</param>
/// <param name="SpellId">Spell for <see cref="CombatActionKind.Cast"/>.</param>
/// <param name="ItemIndex">Backpack index for <see cref="CombatActionKind.UseItem"/>.</param>
/// <param name="Overcharge">Sorcerers: cast the spell overcharged (half again the power, double the spell points).</param>
public sealed record CombatAction(CombatActionKind Kind, int Target = -1, int Ally = -1, string? SpellId = null, int ItemIndex = -1, bool Overcharge = false);

/// <summary>Spoils of a won battle.</summary>
/// <param name="Experience">Experience awarded to each eligible member.</param>
/// <param name="Gold">Gold found.</param>
/// <param name="Gems">Gems found.</param>
/// <param name="Items">Items found.</param>
public sealed record CombatRewards(int Experience, int Gold, int Gems, IReadOnlyList<ItemInstance> Items);
