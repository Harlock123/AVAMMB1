using AVAMMB1.Core.Characters;
using AVAMMB1.Core.Content;
using AVAMMB1.Core.Rules;

namespace AVAMMB1.Core.Items;

/// <summary>Compares gear with what a character wears, and spots junk worth selling.</summary>
public static class ItemCompare
{
    /// <summary>A rough worth of a piece of gear (higher is better) for comparing items in the same slot.</summary>
    /// <param name="d">Item.</param>
    public static double Score(ItemDef d)
    {
        var extras = d.HitBonus * 0.5 + d.StatBonuses.Values.Sum() * 0.5 + (d.Element != Element.Physical ? 1 : 0);
        return d.Kind switch
        {
            ItemKind.Weapon or ItemKind.Missile => d.Damage.Count * (d.Damage.Sides + 1) / 2.0 + d.Damage.Bonus + d.DamageBonus + extras,
            ItemKind.Lantern => d.LightRadius + (d.FuelCapacity == 0 ? 1 : 0),
            _ => d.ArmorClass + extras,
        };
    }

    /// <summary>The worth of what a character now has in the slot an item would take (a two-handed weapon also replaces the shield).</summary>
    private static double Current(Rulebook rules, Character c, ItemDef d)
    {
        var score = c.Equipment.TryGetValue(d.Slot!.Value, out var worn) ? Score(rules.Def(worn)) : 0;
        if (d.TwoHanded && c.Equipment.TryGetValue(EquipSlot.Shield, out var shield))
        {
            score += Score(rules.Def(shield));
        }
        return score;
    }

    /// <summary>Whether the item would be better than what the character uses now (and they can use it).</summary>
    /// <param name="rules">Rulebook.</param>
    /// <param name="c">Character.</param>
    /// <param name="d">Item.</param>
    public static bool IsUpgradeFor(Rulebook rules, Character c, ItemDef d) =>
        d.Slot is not null && Rulebook.CanUse(c, d) && Score(d) > Current(rules, c, d) + 0.01;

    /// <summary>"Better than Long Sword (Dmg 1d8 -> 2d6)", "Worse than ...", "Nothing worn" - or "" for non-gear.</summary>
    /// <param name="rules">Rulebook.</param>
    /// <param name="c">Character.</param>
    /// <param name="d">Item.</param>
    public static string Describe(Rulebook rules, Character c, ItemDef d)
    {
        if (d.Slot is not { } slot)
        {
            return "";
        }
        if (!Rulebook.CanUse(c, d))
        {
            return $"{c.Name} cannot use it";
        }
        if (!c.Equipment.TryGetValue(slot, out var wornItem))
        {
            return $"{c.Name} has nothing in the {slot} slot - an upgrade";
        }
        var worn = rules.Def(wornItem);
        var diff = Score(d) - Current(rules, c, d);
        var verdict = diff > 0.01 ? "Better than" : diff < -0.01 ? "Worse than" : "About the same as";
        var changes = new List<string>();
        if (d.Kind is ItemKind.Weapon or ItemKind.Missile)
        {
            changes.Add($"Dmg {worn.Damage}{(worn.DamageBonus != 0 ? $"+{worn.DamageBonus}" : "")} -> {d.Damage}{(d.DamageBonus != 0 ? $"+{d.DamageBonus}" : "")}");
        }
        if (d.ArmorClass != worn.ArmorClass)
        {
            changes.Add($"AC +{worn.ArmorClass} -> +{d.ArmorClass}");
        }
        if (d.HitBonus != worn.HitBonus)
        {
            changes.Add($"Hit +{worn.HitBonus} -> +{d.HitBonus}");
        }
        if (d.LightRadius != worn.LightRadius)
        {
            changes.Add($"light {worn.LightRadius} -> {d.LightRadius}");
        }
        if (d.TwoHanded && c.Equipment.ContainsKey(EquipSlot.Shield))
        {
            changes.Add("two-handed: the shield comes off");
        }
        return $"{verdict} {c.Name}'s {worn.Name}" + (changes.Count > 0 ? $" ({string.Join(", ", changes)})" : "");
    }

    /// <summary>
    /// Junk: gear (weapons, armour, rings...) that nobody in the party could use as an upgrade. Lanterns,
    /// consumables and quest items are never junk.
    /// </summary>
    /// <param name="rules">Rulebook.</param>
    /// <param name="party">Party.</param>
    /// <param name="d">Item.</param>
    public static bool IsJunk(Rulebook rules, IEnumerable<Character> party, ItemDef d) =>
        d.Slot is not null && d.Kind is not (ItemKind.Lantern or ItemKind.Quest)
        && !party.Any(c => c.IsAlive && IsUpgradeFor(rules, c, d));
}
