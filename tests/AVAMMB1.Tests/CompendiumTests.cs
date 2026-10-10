using AVAMMB1.Core.Combat;
using AVAMMB1.Core.Items;
using AVAMMB1.Core.Session;

namespace AVAMMB1.Tests;

/// <summary>The bestiary and item compendium.</summary>
public class CompendiumTests
{
    [Fact]
    public void Kills_AreCountedPerMonster()
    {
        var s = TestContent.StartedSession();
        s.StartCombat(CombatEngine.Spawn(s.Content.Monster("cellar_rat"), 3, s.Random), new StepResult());
        new Walker(s) { Overwhelm = true }.Fight();
        s.StartCombat(CombatEngine.Spawn(s.Content.Monster("cellar_rat"), 2, s.Random), new StepResult());
        new Walker(s) { Overwhelm = true }.Fight();
        Assert.Equal(5, s.State.Kills["cellar_rat"]);
        Assert.Contains("cellar_rat", s.State.KnownMonsters);
    }

    [Fact]
    public void SeenItems_IncludeWhatThePartyCarries_AndShopStock()
    {
        var s = TestContent.StartedSession();
        Assert.Contains(s.State.Party[0].Equipment.Values.First().ItemId, s.State.SeenItems); // noted on the first look around
        s.State.Party[0].Backpack.Add(new ItemInstance("runeblade"));
        s.Explore();
        Assert.Contains("runeblade", s.State.SeenItems);
        s.NoteSeenItems(["crystal_plate", "no_such_item"]);
        Assert.Contains("crystal_plate", s.State.SeenItems);
        Assert.DoesNotContain("no_such_item", s.State.SeenItems);
    }

    [Fact]
    public void WhereFound_ListsOnlyExploredMaps()
    {
        var s = TestContent.StartedSession();
        Assert.Empty(s.WhereFound("ice_devil"));
        s.State.Explored["rime1"] = "1";
        Assert.Equal(["The Rime Halls"], s.WhereFound("ice_devil"));
        s.State.Explored["cellars"] = "1";
        Assert.Contains("Brindlemoor Cellars", s.WhereFound("kobold_chief")); // a guardian, not a wanderer
    }

    [Fact]
    public void Lore_DescribesHowAMonsterFights()
    {
        var db = TestContent.Content;
        var rime = db.Monster("rimefang");
        Assert.Contains("unique", MonsterLore.Stats(rime), StringComparison.Ordinal);
        Assert.Contains("bites 3d10", MonsterLore.Attacks(rime), StringComparison.Ordinal);
        Assert.Contains("a breath of endless winter (7d8 cold, the whole party)", MonsterLore.Attacks(rime), StringComparison.Ordinal);
        Assert.Contains("immune to cold", MonsterLore.Defenses(rime), StringComparison.Ordinal);
        Assert.Contains("vulnerable to fire", MonsterLore.Defenses(rime), StringComparison.Ordinal);
        Assert.Equal("no special defences", MonsterLore.Defenses(db.Monster("cellar_rat")));
        Assert.Contains("may leave you diseased", MonsterLore.Attacks(db.Monster("cellar_rat")), StringComparison.Ordinal);
    }
}
