using AVAMMB1.Core.Characters;
using AVAMMB1.Core.Rules;

namespace AVAMMB1.Core.Items;

/// <summary>Outcome of an inventory operation.</summary>
/// <param name="Success">Whether the operation succeeded.</param>
/// <param name="Message">Human readable explanation.</param>
public sealed record InventoryResult(bool Success, string Message)
{
    /// <summary>Creates a successful result.</summary>
    /// <param name="message">Message.</param>
    public static InventoryResult Ok(string message) => new(true, message);

    /// <summary>Creates a failed result.</summary>
    /// <param name="message">Message.</param>
    public static InventoryResult Fail(string message) => new(false, message);
}

/// <summary>Equip, unequip, give and discard items.</summary>
/// <param name="rules">The rulebook.</param>
public sealed class Inventory(Rulebook rules)
{
    /// <summary>Adds an item to a character's backpack.</summary>
    /// <param name="c">Character.</param>
    /// <param name="item">Item to add.</param>
    public InventoryResult Add(Character c, ItemInstance item)
    {
        if (c.BackpackFull)
        {
            return InventoryResult.Fail($"{c.Name}'s pack is full.");
        }
        c.Backpack.Add(item);
        return InventoryResult.Ok($"{c.Name} receives {rules.Def(item).Name}.");
    }

    /// <summary>Equips the item at a backpack index, swapping out whatever occupied the slot.</summary>
    /// <param name="c">Character.</param>
    /// <param name="backpackIndex">Index in <see cref="Character.Backpack"/>.</param>
    public InventoryResult Equip(Character c, int backpackIndex)
    {
        if (backpackIndex < 0 || backpackIndex >= c.Backpack.Count)
        {
            return InventoryResult.Fail("No such item.");
        }
        var item = c.Backpack[backpackIndex];
        var def = rules.Def(item);
        if (def.Slot is not { } slot)
        {
            return InventoryResult.Fail($"{def.Name} cannot be equipped.");
        }
        if (!Rulebook.CanUse(c, def))
        {
            return InventoryResult.Fail($"A {rules.Content.Class(c.Class).Name} cannot use {def.Name}.");
        }
        if (slot == EquipSlot.Shield && c.Equipment.TryGetValue(EquipSlot.Weapon, out var w) && rules.Def(w).TwoHanded)
        {
            return InventoryResult.Fail($"{c.Name} needs both hands for {rules.Def(w).Name}.");
        }
        var dropsShield = def.TwoHanded && c.Equipment.ContainsKey(EquipSlot.Shield);
        if (dropsShield && c.Equipment.ContainsKey(slot) && c.Backpack.Count >= Character.BackpackSize)
        {
            return InventoryResult.Fail($"{c.Name} has no room to stow the shield.");
        }
        c.Backpack.RemoveAt(backpackIndex);
        if (c.Equipment.Remove(slot, out var previous))
        {
            c.Backpack.Insert(backpackIndex, previous);
        }
        if (dropsShield && c.Equipment.Remove(EquipSlot.Shield, out var shield))
        {
            c.Backpack.Add(shield);
        }
        c.Equipment[slot] = item;
        RefreshSp(c);
        return InventoryResult.Ok(def.Kind == ItemKind.Lantern
            ? $"{c.Name} equips {def.Name}. It lights the way {def.LightRadius} squares ahead in dark places{(def.FuelCapacity > 0 ? " while it has oil - use a Flask of Oil to top it up" : "")}."
            : $"{c.Name} equips {def.Name}.");
    }

    /// <summary>Moves an equipped item back into the backpack.</summary>
    /// <param name="c">Character.</param>
    /// <param name="slot">Slot to clear.</param>
    public InventoryResult Unequip(Character c, EquipSlot slot)
    {
        if (!c.Equipment.TryGetValue(slot, out var item))
        {
            return InventoryResult.Fail("Nothing equipped there.");
        }
        if (c.BackpackFull)
        {
            return InventoryResult.Fail($"{c.Name}'s pack is full.");
        }
        c.Equipment.Remove(slot);
        c.Backpack.Add(item);
        RefreshSp(c);
        return InventoryResult.Ok($"{c.Name} removes {rules.Def(item).Name}.");
    }

    /// <summary>Gives an item from one character's backpack to another.</summary>
    /// <param name="from">Giver.</param>
    /// <param name="backpackIndex">Index in the giver's backpack.</param>
    /// <param name="to">Receiver.</param>
    public InventoryResult Give(Character from, int backpackIndex, Character to)
    {
        if (backpackIndex < 0 || backpackIndex >= from.Backpack.Count)
        {
            return InventoryResult.Fail("No such item.");
        }
        if (to.BackpackFull)
        {
            return InventoryResult.Fail($"{to.Name}'s pack is full.");
        }
        var item = from.Backpack[backpackIndex];
        from.Backpack.RemoveAt(backpackIndex);
        to.Backpack.Add(item);
        return InventoryResult.Ok($"{from.Name} gives {rules.Def(item).Name} to {to.Name}.");
    }

    /// <summary>Destroys an item in the backpack.</summary>
    /// <param name="c">Character.</param>
    /// <param name="backpackIndex">Index.</param>
    public InventoryResult Discard(Character c, int backpackIndex)
    {
        if (backpackIndex < 0 || backpackIndex >= c.Backpack.Count)
        {
            return InventoryResult.Fail("No such item.");
        }
        var def = rules.Def(c.Backpack[backpackIndex]);
        if (def.Kind == ItemKind.Quest)
        {
            return InventoryResult.Fail($"{def.Name} seems too important to throw away.");
        }
        c.Backpack.RemoveAt(backpackIndex);
        return InventoryResult.Ok($"{c.Name} discards {def.Name}.");
    }

    /// <summary>Whether anyone in a group carries (or wears) an item.</summary>
    /// <param name="members">Characters to search.</param>
    /// <param name="itemId">Item id.</param>
    public static bool AnyoneHas(IEnumerable<Character> members, string itemId) =>
        members.Any(m => m.Backpack.Any(i => i.ItemId == itemId) || m.Equipment.Values.Any(i => i.ItemId == itemId));

    /// <summary>Removes the first copy of an item from the group.</summary>
    /// <param name="members">Characters to search.</param>
    /// <param name="itemId">Item id.</param>
    public static bool RemoveFirst(IEnumerable<Character> members, string itemId)
    {
        foreach (var m in members)
        {
            var idx = m.Backpack.FindIndex(i => i.ItemId == itemId);
            if (idx >= 0)
            {
                m.Backpack.RemoveAt(idx);
                return true;
            }
            var slot = m.Equipment.FirstOrDefault(kv => kv.Value.ItemId == itemId);
            if (slot.Value is not null)
            {
                m.Equipment.Remove(slot.Key);
                return true;
            }
        }
        return false;
    }

    private void RefreshSp(Character c)
    {
        c.MaxSp = rules.ComputeMaxSp(c);
        c.Sp = Math.Min(c.Sp, c.MaxSp);
    }
}
