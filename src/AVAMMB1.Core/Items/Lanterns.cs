using AVAMMB1.Core.Characters;
using AVAMMB1.Core.Rules;

namespace AVAMMB1.Core.Items;

/// <summary>Lantern helpers: which lantern lights the party, and refilling.</summary>
public static class Lanterns
{
    /// <summary>Whether a character's equipped lantern gives light now (has oil, or never needs it).</summary>
    /// <param name="rules">Rules.</param>
    /// <param name="c">Character.</param>
    public static bool IsLit(Rulebook rules, Character c) =>
        c.IsAlive && c.Equipment.TryGetValue(EquipSlot.Light, out var l) && (rules.Def(l).FuelCapacity == 0 || l.Charges > 0);

    /// <summary>Whether a character has an oil-burning lantern equipped.</summary>
    /// <param name="rules">Rules.</param>
    /// <param name="c">Character.</param>
    public static bool Refillable(Rulebook rules, Character c) =>
        c.Equipment.TryGetValue(EquipSlot.Light, out var l) && rules.Def(l).FuelCapacity > 0;

    /// <summary>
    /// The lantern lighting the party: the brightest lit one; among equals one that never needs oil,
    /// then the fullest.
    /// </summary>
    /// <param name="rules">Rules.</param>
    /// <param name="party">Party.</param>
    public static (Character Holder, ItemInstance Lantern)? Active(Rulebook rules, IEnumerable<Character> party) =>
        party.Where(c => IsLit(rules, c))
            .Select(c => (Holder: c, Lantern: c.Equipment[EquipSlot.Light]))
            .OrderByDescending(x => rules.Def(x.Lantern).LightRadius)
            .ThenBy(x => rules.Def(x.Lantern).FuelCapacity == 0 ? 0 : 1)
            .ThenByDescending(x => x.Lantern.Charges)
            .Select(x => ((Character, ItemInstance)?)x)
            .FirstOrDefault();
}
