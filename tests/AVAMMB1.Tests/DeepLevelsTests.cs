using AVAMMB1.Core.Items;
using AVAMMB1.Core.Rules;
using AVAMMB1.Core.Session;

namespace AVAMMB1.Tests;

/// <summary>The deeper levels: the Old Cistern, the Catacombs and the post-game Sunless Deep.</summary>
public class DeepLevelsTests
{
    private static (GameSession S, Walker W) StrongParty(int seed)
    {
        var s = TestContent.StartedSession(seed);
        foreach (var c in s.State.Party)
        {
            c.Level = 20;
            c.MaxHp = c.Hp = 1200;
            c.Stats[Stat.Accuracy] = 25;
            c.Stats[Stat.Might] = 25;
        }
        s.State.Gold = 5000;
        return (s, new Walker(s));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void OldCistern_BossAndHoard(int seed)
    {
        var (s, w) = StrongParty(seed);
        w.Travel("cellars");
        w.Travel("cistern");
        Assert.Equal("cistern", s.State.MapId);
        w.GoTo(e => e.Id == "ooze_mother_fight");
        Assert.Contains("ooze_mother_dead", s.State.Flags);
        Assert.True(Inventory.AnyoneHas(s.State.Party, "drain_buckler"));
        w.GoTo(e => e.Id == "ooze_mother_hoard");
        Assert.True(Inventory.AnyoneHas(s.State.Party, "ring_protection"));
        w.Travel("cellars");
        Assert.Equal("cellars", s.State.MapId);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Catacombs_KeyOpensTheBarrowThrone(int seed)
    {
        var (s, w) = StrongParty(seed);
        s.State.Party[0].Backpack.Add(new ItemInstance("crypt_key"));
        w.Travel("wilds");
        w.Travel("crypt");
        w.Travel("catacombs");
        Assert.Equal("catacombs", s.State.MapId);

        // The throne room is locked until the bone key is taken from the ossuary.
        var throne = s.CurrentMap.AllEvents.First(e => e.Id == "wight_king_fight");
        Assert.Throws<InvalidOperationException>(() => w.Go(throne.X, throne.Y));

        w.GoTo(e => e.Id == "catacomb_ossuary");
        w.GoTo(e => e.Id == "barrow_key_chest");
        Assert.True(Inventory.AnyoneHas(s.State.Party, "barrow_key"));
        w.GoTo(e => e.Id == "wight_king_fight");
        Assert.Contains("wight_king_dead", s.State.Flags);
        Assert.True(Inventory.AnyoneHas(s.State.Party, "gravewarden_blade"));
        w.GoTo(e => e.Id == "wight_king_hoard");
        w.Travel("crypt");
        Assert.Equal("crypt", s.State.MapId);
    }

    [Fact]
    public void SunlessDeep_IsSealedUntilTheSunKingFalls()
    {
        var (s, w) = StrongParty(3);
        w.Travel("wilds");
        w.Travel("saltreach");
        w.Travel("ashkar");
        w.Travel("sunscar");
        w.Travel("tomb1");
        w.Travel("tomb2");
        var refused = w.Travel("deep");
        Assert.Equal("tomb2", s.State.MapId);
        Assert.Contains(refused.Messages, m => m.Text.Contains("seal", StringComparison.OrdinalIgnoreCase));

        w.GoTo(e => e.Id == "sun_king_fight");
        Assert.Contains("sun_king_slain", s.State.Flags);
        w.Travel("deep");
        Assert.Equal("deep", s.State.MapId);
        w.GoTo(e => e.Id == "umbral_wyrm_fight");
        Assert.Contains("umbral_wyrm_slain", s.State.Flags);
        Assert.True(Inventory.AnyoneHas(s.State.Party, "umbral_blade"));
        w.GoTo(e => e.Id == "umbral_wyrm_hoard");
        Assert.True(Inventory.AnyoneHas(s.State.Party, "ring_deep"));
        w.Travel("tomb2");
        Assert.Equal("tomb2", s.State.MapId);
    }
}
