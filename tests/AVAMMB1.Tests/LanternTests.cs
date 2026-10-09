using AVAMMB1.Core.Characters;
using AVAMMB1.Core.Combat;
using AVAMMB1.Core.Content;
using AVAMMB1.Core.Items;
using AVAMMB1.Core.Rules;
using AVAMMB1.Core.Session;

namespace AVAMMB1.Tests;

/// <summary>Light radius tiers, lanterns, oil.</summary>
public class LanternTests
{
    private static GameSession InCellars()
    {
        var s = TestContent.StartedSession();
        var tp = s.Content.Map("cellars").AllEvents.First(e => e.Type == MapEventKind.Teleport);
        (s.State.MapId, s.State.X, s.State.Y) = ("cellars", tp.X, tp.Y);
        s.State.LightSteps = 0;
        return s;
    }

    private static ItemInstance Equip(GameSession s, Character c, string id)
    {
        var item = new ItemInstance(id, s.Content.Item(id).Charges);
        c.Backpack.Add(item);
        Assert.True(s.Inventory.Equip(c, c.Backpack.Count - 1).Success);
        return item;
    }

    [Fact]
    public void Brightness_Tiers_DarkTorchSpellLantern()
    {
        var s = InCellars();
        Assert.True(s.IsDarkHere);
        Assert.Equal(1, s.ViewDistance);

        s.State.AddLight(150, 5); // a torch
        Assert.Equal(5, s.ViewDistance);
        s.State.AddLight(200, 6); // Holy Light: brighter light wins, steps add up
        Assert.Equal(6, s.ViewDistance);
        Assert.Equal(350, s.State.LightSteps);

        Equip(s, s.State.Party[2], "lantern");
        Assert.Equal(8, s.ViewDistance);
        Assert.True(s.LanternLit);

        // Saves from before lanterns: light with no recorded radius counts as the old 6.
        var t = InCellars();
        t.State.LightSteps = 100;
        t.State.LightRadius = 0;
        Assert.Equal(6, t.ViewDistance);
    }

    [Fact]
    public void Lantern_BurnsOilOnlyInDarkPlaces_AndWarns()
    {
        var s = InCellars();
        var lantern = Equip(s, s.State.Party[0], "lantern");
        Assert.Equal(1500, lantern.Charges);
        s.TurnRight();
        for (var i = 0; i < 4 && !s.Move(MoveKind.Forward).Moved; i++)
        {
            s.TurnRight();
        }
        Assert.True(lantern.Charges < 1500);

        // No burning in town.
        var town = TestContent.StartedSession();
        var tl = Equip(town, town.State.Party[0], "lantern");
        for (var i = 0; i < 4; i++)
        {
            town.TurnRight();
            town.Move(MoveKind.Forward);
        }
        Assert.Equal(1500, tl.Charges);

        // Warnings at the low mark and when it runs dry; then the party is in the dark again.
        List<GameMessage> RestUndisturbed()
        {
            for (var i = 0; i < 20; i++)
            {
                var r = s.Rest();
                if (s.Combat is not null)
                {
                    foreach (var c in s.State.Party) { c.Level = 20; c.MaxHp = c.Hp = 500; c.Stats[Stat.Might] = 25; c.Stats[Stat.Accuracy] = 25; }
                    new Walker(s).Fight();
                    continue;
                }
                return r.Messages;
            }
            throw new InvalidOperationException("Could not rest.");
        }
        lantern.Charges = GameSession.LanternLowWarning + 20; // a rest burns 50 steps
        Assert.Contains(RestUndisturbed(), m => m.Text.Contains("running low", StringComparison.Ordinal));
        lantern.Charges = 10;
        Assert.Contains(RestUndisturbed(), m => m.Text.Contains("sputters", StringComparison.Ordinal));
        Assert.Equal(0, lantern.Charges);
        Assert.True(s.IsDarkHere);
    }

    [Fact]
    public void Oil_RefillsTheLantern_UpToFull()
    {
        var s = InCellars();
        var holder = s.State.Party[1];
        var lantern = Equip(s, holder, "lantern");
        lantern.Charges = 100;
        var user = s.State.Party[4];
        user.Backpack.Add(new ItemInstance("oil_flask"));
        var r = s.Spells.UseItem(user, user.Backpack.Count - 1, s.State, null, -1, null);
        Assert.True(r.Success);
        Assert.Equal(850, lantern.Charges);
        Assert.DoesNotContain(user.Backpack, i => i.ItemId == "oil_flask");

        lantern.Charges = 1400;
        user.Backpack.Add(new ItemInstance("oil_flask"));
        s.Spells.UseItem(user, user.Backpack.Count - 1, s.State, null, -1, null);
        Assert.Equal(1500, lantern.Charges);

        // Full lantern, or nobody with a lantern: the flask is kept.
        user.Backpack.Add(new ItemInstance("oil_flask"));
        Assert.False(s.Spells.UseItem(user, user.Backpack.Count - 1, s.State, null, -1, null).Success);
        var t = InCellars();
        t.State.Party[0].Backpack.Add(new ItemInstance("oil_flask"));
        Assert.False(t.Spells.UseItem(t.State.Party[0], t.State.Party[0].Backpack.Count - 1, t.State, null, -1, null).Success);
        Assert.Contains(t.State.Party[0].Backpack, i => i.ItemId == "oil_flask");
    }

    [Fact]
    public void Oil_FillsALanternStillInABackpack()
    {
        var s = InCellars();
        var owner = s.State.Party[2];
        var lantern = new ItemInstance("lantern", 200);
        owner.Backpack.Add(lantern);
        var user = s.State.Party[0];
        user.Backpack.Add(new ItemInstance("oil_flask"));
        var r = s.Spells.UseItem(user, user.Backpack.Count - 1, s.State, null, -1, null);
        Assert.True(r.Success);
        Assert.Equal(950, lantern.Charges);
        Assert.Contains(owner.Name, r.Messages[0].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void EverburningLantern_NeverRunsOut()
    {
        var s = InCellars();
        var lantern = Equip(s, s.State.Party[0], "lantern_everburning");
        for (var i = 0; i < 6; i++)
        {
            s.Rest();
        }
        Assert.Equal(0, lantern.Charges);
        Assert.Equal(8, s.ViewDistance);
        Assert.Contains(s.Content.Map("rime1").AllEvents, e => e.Items.Contains("lantern_everburning"));
    }

    [Fact]
    public void Oil_ThrownInBattle_BurnsAMonster()
    {
        var s = InCellars();
        var beast = CombatEngine.Spawn(s.Content.Monster("ice_beast"), 1, s.Random).Single(); // takes extra fire damage
        s.StartCombat([beast], new StepResult());
        var combat = s.Combat!;
        combat.Advance();
        var c = combat.ActiveCharacter!;
        c.Backpack.Add(new ItemInstance("oil_flask"));
        var hp = beast.Hp;
        combat.Act(new CombatAction(CombatActionKind.UseItem, Target: 0, ItemIndex: c.Backpack.Count - 1));
        Assert.True(beast.Hp < hp);
        Assert.DoesNotContain(c.Backpack, i => i.ItemId == "oil_flask");
    }

    [Fact]
    public void BrighterLight_MapsFurtherDownTheCorridor()
    {
        int Explored(GameSession s) => s.State.Explored.TryGetValue("cellars", out var bits) ? bits.Count(ch => ch == '1') : 0;
        var dark = InCellars();
        var lit = InCellars();
        Equip(lit, lit.State.Party[0], "lantern");
        foreach (var s in new[] { dark, lit })
        {
            for (var i = 0; i < 4; i++)
            {
                s.TurnRight();
            }
        }
        Assert.True(Explored(lit) > Explored(dark), $"lit {Explored(lit)} vs dark {Explored(dark)}");
    }

    [Fact]
    public void LanternsAndOil_AreSoldAndFound()
    {
        var db = TestContent.Content;
        Assert.Contains(db.Shops.Values, sh => sh.Stock.Contains("lantern"));
        Assert.Contains(db.Shops["brindle_apothecary"].Stock, i => i == "oil_flask");
        Assert.Equal(EquipSlot.Light, db.Item("lantern").Slot);
        Assert.Equal(5, db.Item("torch").LightRadius);
        Assert.All(new[] { "c_light", "s_light", "i_light" }, id => Assert.Equal(6, db.Spell(id).LightRadius));
    }
}
