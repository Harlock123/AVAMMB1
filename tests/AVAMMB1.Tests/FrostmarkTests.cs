using AVAMMB1.Core.Combat;
using AVAMMB1.Core.Content;
using AVAMMB1.Core.Items;
using AVAMMB1.Core.Rules;
using AVAMMB1.Core.Session;

namespace AVAMMB1.Tests;

/// <summary>The Frostmark (Wintermere, the tundra, the Rime Halls), town bounties, academies and elites.</summary>
public class FrostmarkTests
{
    private static (GameSession S, Walker W) StrongParty(int seed)
    {
        var s = TestContent.StartedSession(seed);
        foreach (var c in s.State.Party)
        {
            c.Level = 20;
            c.MaxHp = c.Hp = 1500;
            c.Stats[Stat.Accuracy] = 25;
            c.Stats[Stat.Might] = 25;
        }
        s.State.Gold = 20000;
        return (s, new Walker(s));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Wintermere_ShipTundraAndRimefang(int seed)
    {
        var (s, w) = StrongParty(seed);
        w.Travel("wilds");
        w.Travel("saltreach");
        var gold = s.State.Gold;
        w.Travel("wintermere");
        Assert.Equal("wintermere", s.State.MapId);
        Assert.Equal(gold - 200, s.State.Gold);

        w.GoTo(e => e.Id == "jarl_intro");
        w.GoTo(e => e.Id == "aldous_intro");
        Assert.Contains("jarl_met", s.State.Flags);
        Assert.Contains("aldous_met", s.State.Flags);

        w.Travel("frostmark");
        w.GoTo(e => e.Id == "expedition_camp");
        w.GoTo(e => e.Id == "expedition_cache");
        Assert.True(Inventory.AnyoneHas(s.State.Party, "frozen_journal"));

        w.Travel("rime1");
        w.Travel("rime2");
        w.GoTo(e => e.Id == "rimefang_fight");
        Assert.Contains("rimefang_slain", s.State.Flags);
        Assert.True(Inventory.AnyoneHas(s.State.Party, "rimefang_heart"));
        Assert.True(Inventory.AnyoneHas(s.State.Party, "rimescale_mail"));
        w.GoTo(e => e.Id == "rimefang_hoard");

        w.Travel("rime1");
        w.Travel("frostmark");
        w.Travel("wintermere");
        w.GoTo(e => e.Id == "aldous_reward");
        w.Go(7, 9); // step away so the same square can be entered again
        w.GoTo(e => e.Id == "jarl_reward");
        Assert.Contains("journal_returned", s.State.Flags);
        Assert.Contains("rimefang_heart_returned", s.State.Flags);
        Assert.True(Inventory.AnyoneHas(s.State.Party, "ring_winter"));
        Assert.Contains(QuestJournal.Quests(s.State, s.Content), q => q.Id == "rimefang" && q.Done);
    }

    [Fact]
    public void Bounties_TrophyFromTheHoard_RewardFromTheGiver()
    {
        var (s, w) = StrongParty(3);
        w.GoTo(e => e.Id == "dunmore_intro");
        Assert.Contains("dunmore_met", s.State.Flags);
        w.Travel("wilds");
        w.GoTo(e => e.Id == "wilds_ogre");
        w.GoTo(e => e.Id == "ogre_hoard");
        Assert.True(Inventory.AnyoneHas(s.State.Party, "ogre_tusk"));
        w.Travel("brindlemoor");
        w.GoTo(e => e.Id == "dunmore_reward");
        Assert.Contains("dunmore_done", s.State.Flags);
        Assert.False(Inventory.AnyoneHas(s.State.Party, "ogre_tusk"));

        // Every bounty's trophy is in a hoard, and its giver stands on a walkable town square.
        foreach (var (town, key, trophy) in new[] { ("brindlemoor", "dunmore", "ogre_tusk"), ("saltreach", "quell", "wyvern_egg"),
                     ("thornwick", "brega", "renegade_signet"), ("duskmere", "wren", "barrow_crown"), ("ashkar", "rashid", "scorpion_stinger") })
        {
            var giver = s.Content.Map(town).AllEvents.Single(e => e.Id == key + "_reward");
            Assert.Equal(trophy, giver.RequiresItem);
            Assert.False(s.Content.Map(town).IsSolid(giver.X, giver.Y));
            Assert.Contains(s.Content.Maps.Values.SelectMany(m => m.AllEvents), e => e.Type == MapEventKind.Treasure && e.Items.Contains(trophy));
        }
    }

    [Fact]
    public void Academy_RaisesAStat_PriceRises_AndHasLimits()
    {
        var s = TestContent.StartedSession();
        var ev = s.Content.Map("wintermere").AllEvents.Single(e => e.Type == MapEventKind.Academy);
        var c = s.State.Party[0];
        s.State.Gold = 100000;
        var might = c.BaseStat(Stat.Might);
        Assert.Equal(1000, TownServices.AcademyCost(c, ev));
        s.Town.Study(c, Stat.Might, ev);
        Assert.Equal(might + 1, c.BaseStat(Stat.Might));
        Assert.Equal(2000, TownServices.AcademyCost(c, ev));
        Assert.Equal(100000 - 1000, s.State.TotalGold);

        c.Stats[Stat.Luck] = TownServices.AcademyStatCap;
        Assert.NotNull(TownServices.AcademyBlock(c, Stat.Luck));
        c.AcademyPoints = TownServices.AcademyMaxPoints;
        Assert.NotNull(TownServices.AcademyBlock(c, Stat.Speed));
        var before = s.State.TotalGold;
        s.Town.Study(c, Stat.Speed, ev);
        Assert.Equal(before, s.State.TotalGold);

        // Not enough gold: nothing changes.
        var d = s.State.Party[1];
        s.State.Gold = 10;
        foreach (var m in s.State.Party) { m.Gold = 0; }
        var speed = d.BaseStat(Stat.Speed);
        s.Town.Study(d, Stat.Speed, ev);
        Assert.Equal(speed, d.BaseStat(Stat.Speed));
    }

    [Fact]
    public void Elites_AreTougher_AndWorthMore()
    {
        var s = TestContent.StartedSession();
        var normal = CombatEngine.Spawn(s.Content.Monster("ogre"), 1, s.Random).Single();
        var elite = CombatEngine.Spawn(s.Content.Monster("ogre"), 1, s.Random).Single();
        var hp = elite.MaxHp;
        elite.MakeElite();
        Assert.Equal(hp * 2, elite.MaxHp);
        Assert.Equal(elite.MaxHp, elite.Hp);
        Assert.Equal(normal.ArmorClass + 2, elite.ArmorClass);
        Assert.Equal(15, elite.ScaleDamage(10));

        s.StartCombat([elite], new StepResult());
        Assert.StartsWith("Elite ", elite.Label);
        var xp = s.State.Party[0].Experience;
        foreach (var c in s.State.Party) { c.Level = 20; c.MaxHp = c.Hp = 500; c.Stats[Stat.Accuracy] = 25; c.Stats[Stat.Might] = 25; }
        new Walker(s).Fight();
        Assert.Equal(xp + 3 * s.Content.Monster("ogre").Xp, s.State.Party[0].Experience);
        Assert.All(new[] { 1, 5, 9, 14 }, lvl => Assert.All(CombatEngine.EliteLoot(lvl), id => Assert.True(s.Content.Items.ContainsKey(id), id)));
    }
}
