using AVAMMB1.Core.Dice;
using AVAMMB1.Core.Items;
using AVAMMB1.Core.Rules;
using AVAMMB1.Core.Session;

namespace AVAMMB1.Tests;

/// <summary>Smithy upgrades (+1 to +5).</summary>
public class SmithyTests
{
    [Fact]
    public void Costs_RiseSteeply_AndNeedGems()
    {
        var sword = TestContent.Content.Item("long_sword"); // 50 gold
        Assert.Equal(250, Rulebook.UpgradeGold(sword, 1));
        Assert.Equal(1000, Rulebook.UpgradeGold(sword, 2));
        Assert.Equal(6250, Rulebook.UpgradeGold(sword, 5));
        Assert.Equal(3, Rulebook.UpgradeGems(3));
        Assert.True(Rulebook.Upgradable(sword));
        Assert.False(Rulebook.Upgradable(TestContent.Content.Item("ring_protection")));
        Assert.False(Rulebook.Upgradable(TestContent.Content.Item("lantern")));
    }

    [Fact]
    public void Upgrade_ImprovesHitDamageAndArmor()
    {
        var s = TestContent.StartedSession();
        var smithy = s.Content.Shops["brindle_smithy"];
        var knight = s.State.Party.First(c => c.Class == "knight");
        var sword = knight.Equipment[EquipSlot.Weapon];
        var armor = knight.Equipment[EquipSlot.Armor];
        s.State.Gold = 100_000;
        s.State.Gems = 10;
        var (hit, ac) = (s.Rules.MeleeAttackBonus(knight), s.Rules.ArmorClass(knight));
        var minDamage = Enumerable.Range(0, 50).Min(_ => s.Rules.RollMeleeDamage(knight, new ScriptedRandom(1)));

        Assert.Contains("ready", s.Town.Upgrade(smithy, knight, sword)[0].Text, StringComparison.Ordinal);
        s.Town.Upgrade(smithy, knight, sword);
        s.Town.Upgrade(smithy, knight, armor);

        Assert.Equal(2, sword.Plus);
        Assert.Equal(hit + 2, s.Rules.MeleeAttackBonus(knight));
        Assert.Equal(ac + 1, s.Rules.ArmorClass(knight));
        Assert.Equal(minDamage + 2, s.Rules.RollMeleeDamage(knight, new ScriptedRandom(1)));
        Assert.Equal(10 - 1 - 2 - 1, s.State.Gems);
        Assert.EndsWith("+2", s.Rules.ItemName(sword), StringComparison.Ordinal);
        Assert.True(s.Rules.SellPrice(sword) > Rulebook.SellPrice(s.Content.Item(sword.ItemId)));
    }

    [Fact]
    public void Upgrade_StopsAtFive_AndNeedsASmithyGemsAndGold()
    {
        var s = TestContent.StartedSession();
        var knight = s.State.Party.First(c => c.Class == "knight");
        var sword = knight.Equipment[EquipSlot.Weapon];
        var smithy = s.Content.Shops["brindle_smithy"];

        s.State.Gold = 1_000_000;
        s.State.Gems = 0;
        Assert.Equal(MessageKind.Bad, s.Town.Upgrade(smithy, knight, sword)[0].Kind); // no gems
        Assert.Equal(MessageKind.Bad, s.Town.Upgrade(s.Content.Shops["brindle_apothecary"], knight, sword)[0].Kind); // no smith

        s.State.Gems = 100;
        for (var i = 0; i < 7; i++)
        {
            s.Town.Upgrade(smithy, knight, sword);
        }
        Assert.Equal(Rulebook.MaxPlus, sword.Plus);

        var poor = TestContent.StartedSession();
        var k2 = poor.State.Party.First(c => c.Class == "knight");
        poor.State.Gems = 5;
        poor.State.Gold = 10;
        foreach (var c in poor.State.Party) c.Gold = 0;
        Assert.Equal(MessageKind.Bad, poor.Town.Upgrade(smithy, k2, k2.Equipment[EquipSlot.Weapon]).Last().Kind);
        Assert.Equal(0, k2.Equipment[EquipSlot.Weapon].Plus);
        Assert.Equal(5, poor.State.Gems); // nothing taken
    }

    [Fact]
    public void Upgrades_CountInComparisons_AndSurviveSaves()
    {
        var s = TestContent.StartedSession();
        var knight = s.State.Party.First(c => c.Class == "knight");
        knight.Equipment[EquipSlot.Weapon] = new ItemInstance("long_sword") { Plus = 3 };
        Assert.False(ItemCompare.IsUpgradeFor(s.Rules, knight, s.Content.Item("broad_sword"))); // 6.5 < 4.5 + 4.5
        var json = AVAMMB1.Core.Persistence.SaveGameService.Serialize(new AVAMMB1.Core.Persistence.SaveFile { State = s.State });
        var back = AVAMMB1.Core.Persistence.SaveGameService.Deserialize(json).State.Party.First(c => c.Class == "knight");
        Assert.Equal(3, back.Equipment[EquipSlot.Weapon].Plus);
    }
}
