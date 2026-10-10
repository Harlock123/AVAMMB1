using AVAMMB1.Core.Items;
using AVAMMB1.Core.Rules;

namespace AVAMMB1.Tests;

/// <summary>Item comparison and selling junk.</summary>
public class ShopHelpTests
{
    [Fact]
    public void Compare_TellsBetterFromWorse()
    {
        var s = TestContent.StartedSession();
        var knight = s.State.Party.First(c => c.Class == "knight");
        knight.Equipment[EquipSlot.Weapon] = new ItemInstance("long_sword");
        knight.Equipment[EquipSlot.Shield] = new ItemInstance("small_shield");
        var db = s.Content;

        Assert.True(ItemCompare.IsUpgradeFor(s.Rules, knight, db.Item("broad_sword")));
        Assert.StartsWith("Better than", ItemCompare.Describe(s.Rules, knight, db.Item("broad_sword")), StringComparison.Ordinal);
        Assert.False(ItemCompare.IsUpgradeFor(s.Rules, knight, db.Item("short_sword")));
        Assert.StartsWith("Worse than", ItemCompare.Describe(s.Rules, knight, db.Item("short_sword")), StringComparison.Ordinal);
        Assert.Contains("Dmg 1d8 -> 2d4+1", ItemCompare.Describe(s.Rules, knight, db.Item("broad_sword")), StringComparison.Ordinal);
        Assert.Equal("", ItemCompare.Describe(s.Rules, knight, db.Item("oil_flask"))); // not gear

        var sorcerer = s.State.Party.First(c => c.Class == "sorcerer");
        Assert.Contains("cannot use", ItemCompare.Describe(s.Rules, sorcerer, db.Item("broad_sword")), StringComparison.Ordinal);
    }

    [Fact]
    public void SellJunk_SellsOnlyGearNobodyNeeds()
    {
        var s = TestContent.StartedSession();
        var knight = s.State.Party.First(c => c.Class == "knight");
        s.State.Party.RemoveAll(c => c != knight);
        knight.Equipment.Clear();
        knight.Equipment[EquipSlot.Weapon] = new ItemInstance("long_sword");
        knight.Equipment[EquipSlot.Armor] = new ItemInstance("chain_mail");
        knight.Backpack.Clear();
        knight.Backpack.AddRange([
            new ItemInstance("short_sword"),   // worse than his sword: junk
            new ItemInstance("leather_armor"), // worse than his mail: junk
            new ItemInstance("broad_sword"),   // better: kept
            new ItemInstance("small_shield"),  // fills an empty slot: kept
            new ItemInstance("lantern", 1500), // never junk
            new ItemInstance("oil_flask"),     // not gear
        ]);
        var gold = s.State.Gold;

        var junk = s.Town.Junk().Select(j => j.Item.ItemId).ToList();
        Assert.Equal(["short_sword", "leather_armor"], junk);
        var log = s.Town.SellJunk();

        Assert.Equal(gold + Rulebook.SellPrice(s.Content.Item("short_sword")) + Rulebook.SellPrice(s.Content.Item("leather_armor")), s.State.Gold);
        Assert.Equal(["broad_sword", "small_shield", "lantern", "oil_flask"], knight.Backpack.Select(i => i.ItemId));
        Assert.Contains("2 unneeded items", log[0].Text, StringComparison.Ordinal);
        Assert.Contains("Nobody", s.Town.SellJunk()[0].Text, StringComparison.Ordinal);
    }
}
