using AVAMMB1.Core.Characters;
using AVAMMB1.Core.Items;
using AVAMMB1.Core.Rules;

namespace AVAMMB1.Tests;

public class InventoryTests
{
    [Fact]
    public void Equip_SwapsPreviousItemIntoBackpack()
    {
        var s = TestContent.NewSession();
        var c = TestContent.Make(s, "knight");
        c.Backpack.Add(new ItemInstance("broad_sword"));
        var r = s.Inventory.Equip(c, 0);
        Assert.True(r.Success, r.Message);
        Assert.Equal("broad_sword", c.Equipment[EquipSlot.Weapon].ItemId);
        Assert.Equal("long_sword", c.Backpack.Single().ItemId);
    }

    [Fact]
    public void Equip_RespectsClassRestrictions()
    {
        var s = TestContent.NewSession();
        var c = TestContent.Make(s, "sorcerer");
        c.Backpack.Add(new ItemInstance("plate_mail"));
        var r = s.Inventory.Equip(c, 0);
        Assert.False(r.Success);
        Assert.Equal("padded_armor", c.Equipment[EquipSlot.Armor].ItemId);
    }

    [Fact]
    public void TwoHandedWeapon_RemovesShield_AndBlocksShield()
    {
        var s = TestContent.NewSession();
        var c = TestContent.Make(s, "knight");
        c.Backpack.Add(new ItemInstance("great_sword"));
        Assert.True(s.Inventory.Equip(c, 0).Success);
        Assert.False(c.Equipment.ContainsKey(EquipSlot.Shield));
        Assert.Contains(c.Backpack, i => i.ItemId == "small_shield");
        var shieldIndex = c.Backpack.FindIndex(i => i.ItemId == "small_shield");
        Assert.False(s.Inventory.Equip(c, shieldIndex).Success);
    }

    [Fact]
    public void Backpack_HasCapacity()
    {
        var s = TestContent.NewSession();
        var c = TestContent.Make(s, "robber");
        for (var i = 0; i < Character.BackpackSize; i++)
        {
            Assert.True(s.Inventory.Add(c, new ItemInstance("torch")).Success);
        }
        Assert.False(s.Inventory.Add(c, new ItemInstance("torch")).Success);
        Assert.False(s.Inventory.Unequip(c, EquipSlot.Weapon).Success);
    }

    [Fact]
    public void Give_And_Discard()
    {
        var s = TestContent.NewSession();
        var a = TestContent.Make(s, "knight");
        var b = TestContent.Make(s, "robber");
        a.Backpack.Add(new ItemInstance("potion_healing"));
        a.Backpack.Add(new ItemInstance("crypt_key"));
        Assert.True(s.Inventory.Give(a, 0, b).Success);
        Assert.Equal("potion_healing", b.Backpack.Single().ItemId);
        Assert.False(s.Inventory.Discard(a, 0).Success); // quest item
        Assert.True(Inventory.AnyoneHas([a, b], "crypt_key"));
        Assert.True(Inventory.RemoveFirst([a, b], "crypt_key"));
        Assert.False(Inventory.AnyoneHas([a, b], "crypt_key"));
    }

    [Fact]
    public void Shop_BuyAndSell()
    {
        var s = TestContent.StartedSession();
        var shop = s.Content.Shops["brindle_smithy"];
        var c = s.State.Party[0];
        var gold = s.State.Gold;
        s.Town.Buy(shop, "chain_mail", c);
        Assert.Equal(gold - 150, s.State.Gold);
        var idx = c.Backpack.FindIndex(i => i.ItemId == "chain_mail");
        s.Town.Sell(c, idx);
        Assert.Equal(gold - 150 + 75, s.State.Gold);
        s.Town.Buy(shop, "plate_mail", c); // not stocked here
        Assert.DoesNotContain(c.Backpack, i => i.ItemId == "plate_mail");
    }

    [Fact]
    public void UsingPotion_HealsAndConsumes()
    {
        var s = TestContent.StartedSession();
        var c = s.State.Party[0];
        c.Hp = 1;
        c.Backpack.Add(new ItemInstance("potion_healing"));
        var r = s.Spells.UseItem(c, c.Backpack.Count - 1, s.State, null, 0, null);
        Assert.True(r.Success);
        Assert.True(c.Hp > 1);
        Assert.DoesNotContain(c.Backpack, i => i.ItemId == "potion_healing");
    }

    [Fact]
    public void Wand_UsesCharges()
    {
        var s = TestContent.StartedSession();
        var c = s.State.Party[0];
        c.Backpack.Add(new ItemInstance("wand_lightning", 5));
        var monsters = AVAMMB1.Core.Combat.CombatEngine.Spawn(s.Content.Monster("ogre"), 1, s.Random).ToList();
        var result = new AVAMMB1.Core.Session.StepResult();
        s.StartCombat(monsters, result);
        var r = s.Spells.UseItem(c, c.Backpack.Count - 1, s.State, s.Combat, -1, monsters[0]);
        Assert.True(r.Success);
        Assert.Equal(4, c.Backpack.Last().Charges);
    }
}
