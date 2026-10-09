using AVAMMB1.Core.Content;
using AVAMMB1.Core.Items;
using AVAMMB1.Core.Rules;
using AVAMMB1.Core.Session;

namespace AVAMMB1.Tests;

/// <summary>The Ashen Hills expansion: Thornwick, Duskmere, the mines and the Sunken Temple.</summary>
public class ExpansionTests
{
    [Fact]
    public void World_HasSixTownsAndEveryMapIsConnected()
    {
        var db = TestContent.Content;
        Assert.Equal(6, db.Maps.Values.Count(m => m.Def.Kind == MapKind.Town));
        Assert.True(db.Maps.Values.Count(m => m.Def.Kind == MapKind.Dungeon) >= 6);

        // Follow every teleport from the start map: all maps must be reachable.
        var seen = new HashSet<string> { db.Config.StartMap };
        var queue = new Queue<string>(seen);
        while (queue.Count > 0)
        {
            foreach (var e in db.Map(queue.Dequeue()).AllEvents.Where(e => e.Type == MapEventKind.Teleport && e.Map is not null))
            {
                if (seen.Add(e.Map!))
                {
                    queue.Enqueue(e.Map!);
                }
            }
        }
        Assert.Equal(db.Maps.Keys.OrderBy(k => k), seen.OrderBy(k => k));
    }

    [Fact]
    public void EveryStairway_HasAWayBack()
    {
        var db = TestContent.Content;
        foreach (var map in db.Maps.Values)
        {
            foreach (var e in map.AllEvents.Where(e => e.Type == MapEventKind.Teleport && e.Map is not null && e.Map != map.Id))
            {
                Assert.Contains(db.Map(e.Map!).AllEvents, back => back.Type == MapEventKind.Teleport && back.Map == map.Id);
            }
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void SideQuests_CanBeCompleted(int seed)
    {
        var s = TestContent.StartedSession(seed);
        foreach (var c in s.State.Party)
        {
            c.Level = 15;
            c.MaxHp = c.Hp = 500;
            c.Stats[Stat.Accuracy] = 25;
            c.Stats[Stat.Might] = 25;
        }
        var w = new Walker(s);

        // Brindlemoor -> Greenvale -> the pass -> the Ashen Hills -> Thornwick.
        w.Travel("wilds");
        w.Travel("hills");
        Assert.Equal("hills", s.State.MapId);
        w.Travel("thornwick");
        w.GoTo(e => e.Id == "halvard_intro");
        Assert.Contains("halvard_met", s.State.Flags);

        // Down the mines, slay the wyrm, take the Heartstone back.
        w.Travel("mines1");
        w.Travel("mines2");
        w.GoTo(e => e.Id == "wyrm_fight");
        Assert.Contains("wyrm_slain", s.State.Flags);
        w.GoTo(e => e.Id == "wyrm_hoard");
        Assert.True(Inventory.AnyoneHas(s.State.Party, "heartstone"));
        w.Travel("mines1");
        w.Travel("thornwick");
        w.GoTo(e => e.Id == "halvard_intro");
        Assert.Contains("heartstone_returned", s.State.Flags);
        Assert.True(Inventory.AnyoneHas(s.State.Party, "ring_heartfire"));

        // Duskmere and the Sunken Temple.
        w.Travel("hills");
        w.Travel("duskmere");
        w.GoTo(e => e.Id == "ilsa_intro");
        Assert.Contains("ilsa_met", s.State.Flags);
        w.Travel("hills");
        w.Travel("temple");
        w.GoTo(e => e.Id == "hydra_fight");
        Assert.Contains("hydra_slain", s.State.Flags);
        w.GoTo(e => e.Id == "lotus_garden");
        Assert.True(Inventory.AnyoneHas(s.State.Party, "moon_lotus"));
        w.Travel("hills");
        w.Travel("duskmere");
        w.GoTo(e => e.Id == "ilsa_intro");
        Assert.Contains("lotus_delivered", s.State.Flags);
        Assert.True(Inventory.AnyoneHas(s.State.Party, "lotus_amulet"));
    }

    [Fact]
    public void NewShops_SellTheirStock()
    {
        var s = TestContent.StartedSession();
        s.State.Gold = 5000;
        var c = s.State.Party[0];
        s.Town.Buy(s.Content.Shops["thornwick_forge"], "dwarven_mail", c);
        Assert.Contains(c.Backpack, i => i.ItemId == "dwarven_mail");
        Assert.True(s.Inventory.Equip(c, c.Backpack.FindIndex(i => i.ItemId == "dwarven_mail")).Success);
        Assert.Equal(6 + 1 + 0, s.Rules.ArmorClass(c)); // dwarven mail + small shield + Speed 12
    }
}

/// <summary>The Sunscar Coast: Port Ashkar, the Wastes and the Tomb of the Sun Kings.</summary>
public class SunscarTests
{
    [Fact]
    public void Ferry_ChargesTheFare_AndRefusesWithoutGold()
    {
        var s = TestContent.StartedSession();
        foreach (var c in s.State.Party)
        {
            c.Level = 10;
            c.MaxHp = c.Hp = 300; // the walk there is not what this test is about
        }
        var w = new Walker(s);
        w.Travel("wilds");
        w.Travel("saltreach");
        s.State.Gold = 0; // everyone's own gold is already pooled at the start
        var refused = w.Travel("ashkar");
        Assert.Equal("saltreach", s.State.MapId);
        Assert.Contains(refused.Messages, m => m.Text.Contains("hundred gold", StringComparison.Ordinal));

        s.State.Gold = 150;
        w.Go(s.State.X, s.State.Y + 1); // step off the dock and back on
        w.Travel("ashkar");
        Assert.Equal("ashkar", s.State.MapId);
        Assert.Equal(50, s.State.Gold);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void SunDiskQuest_CanBeCompleted(int seed)
    {
        var s = TestContent.StartedSession(seed);
        foreach (var c in s.State.Party)
        {
            c.Level = 18;
            c.MaxHp = c.Hp = 900;
            c.Stats[AVAMMB1.Core.Rules.Stat.Accuracy] = 25;
            c.Stats[AVAMMB1.Core.Rules.Stat.Might] = 25;
        }
        s.State.Gold = 5000;
        var w = new Walker(s);
        w.Travel("wilds");
        w.Travel("saltreach");
        w.Travel("ashkar");
        w.GoTo(e => e.Id == "tamsin_intro");
        Assert.Contains("tamsin_met", s.State.Flags);
        w.Travel("sunscar");
        w.Travel("tomb1");
        w.Travel("tomb2");
        w.GoTo(e => e.Id == "sun_king_fight");
        Assert.Contains("sun_king_slain", s.State.Flags);
        w.GoTo(e => e.Id == "sun_king_hoard");
        Assert.True(AVAMMB1.Core.Items.Inventory.AnyoneHas(s.State.Party, "sun_disk"));
        w.Travel("tomb1");
        w.Travel("sunscar");
        w.Travel("ashkar");
        w.GoTo(e => e.Id == "tamsin_intro");
        Assert.Contains("sun_disk_returned", s.State.Flags);
        Assert.True(AVAMMB1.Core.Items.Inventory.AnyoneHas(s.State.Party, "scarab_amulet"));
    }
}
