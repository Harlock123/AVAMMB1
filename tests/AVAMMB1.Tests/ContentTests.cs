using AVAMMB1.Core.Content;
using AVAMMB1.Core.Rules;
using AVAMMB1.Core.World;

namespace AVAMMB1.Tests;

public class ContentTests
{
    [Fact]
    public void EmbeddedContent_LoadsAndValidates()
    {
        var db = TestContent.Content;
        Assert.Empty(db.FindProblems());
        Assert.Equal(5, db.Races.Count);
        Assert.Equal(6, db.Classes.Count);
        Assert.True(db.Monsters.Count >= 20);
        Assert.True(db.Items.Count >= 40);
        Assert.True(db.Maps.Count >= 6);
    }

    [Fact]
    public void EnumsAndFlags_ParseCaseInsensitively()
    {
        var db = TestContent.Content;
        Assert.Equal(2, db.Race("elf").StatModifiers[Stat.Intellect]);
        Assert.Equal(Condition.Poisoned, db.Monster("giant_spider").Attacks[0].Inflicts);
        var cure = db.Spell("c_cure").Conditions;
        Assert.True(cure.HasFlag(Condition.Paralyzed) && cure.HasFlag(Condition.Asleep));
        Assert.Equal(SpellSchool.Sorcerer, db.Spell("s_fireball").School);
    }

    [Fact]
    public void AllEvents_AreReachableFromEachMapEntrance()
    {
        var db = TestContent.Content;
        foreach (var map in db.Maps.Values)
        {
            // Every map has at least one teleport/stairs; use the first as the entrance.
            var entry = map.AllEvents.First(e => e.Type == MapEventKind.Teleport);
            var reachable = map.Reachable(entry.X, entry.Y);
            foreach (var ev in map.AllEvents)
            {
                Assert.True(reachable.Contains((ev.X, ev.Y)), $"{map.Id}: event at ({ev.X},{ev.Y}) unreachable");
            }
        }
    }

    [Fact]
    public void Teleports_LeadToWalkableCells()
    {
        var db = TestContent.Content;
        foreach (var map in db.Maps.Values)
        {
            foreach (var ev in map.AllEvents.Where(e => e.Type == MapEventKind.Teleport))
            {
                var dest = db.Map(ev.Map!);
                Assert.False(dest.IsSolid(ev.ToX, ev.ToY), $"{map.Id} -> {dest.Id}");
            }
        }
    }

    [Fact]
    public void EdgeMap_ParsesWallsAndDoors()
    {
        var def = new MapDef
        {
            Id = "t", Width = 2, Height = 1, Format = MapFormat.Edges,
            Grid = ["+-+-+", "|.D.|", "+-+-+"],
        };
        var map = GameMap.Parse(def);
        Assert.Equal(WallKind.Wall, map.GetWall(0, 0, Direction.West));
        Assert.Equal(WallKind.Door, map.GetWall(0, 0, Direction.East));
        Assert.Equal(WallKind.Door, map.GetWall(1, 0, Direction.West));
        Assert.Equal(WallKind.Wall, map.GetWall(1, 0, Direction.North));
        Assert.Equal(2, map.Reachable(0, 0).Count);
    }

    [Fact]
    public void BadMapDimensions_Throw()
    {
        var def = new MapDef { Id = "bad", Width = 3, Height = 2, Format = MapFormat.Blocks, Grid = ["...", ".."] };
        Assert.Throws<InvalidDataException>(() => GameMap.Parse(def));
    }

    private static string RepoAssets()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AVAMMB1.sln")))
        {
            dir = dir.Parent;
        }
        return Path.Combine(dir?.FullName ?? throw new DirectoryNotFoundException("repo root"), "Assets");
    }

    [Fact]
    public void EveryMap_HasExistingMusicAndAmbience()
    {
        var assets = RepoAssets();
        foreach (var map in TestContent.Content.Maps.Values)
        {
            Assert.True(File.Exists(Path.Combine(assets, "Audio", "Music", map.Def.Music + ".ogg")), $"{map.Id}: music {map.Def.Music}");
            Assert.False(string.IsNullOrEmpty(map.Def.Ambience), $"{map.Id}: no ambience");
            Assert.True(File.Exists(Path.Combine(assets, "Audio", "Ambience", map.Def.Ambience + ".ogg")), $"{map.Id}: ambience {map.Def.Ambience}");
        }
        Assert.True(File.Exists(Path.Combine(assets, "Audio", "Ambience", "night.ogg")));
        foreach (var track in new[] { "title", "battle", "boss" })
        {
            Assert.True(File.Exists(Path.Combine(assets, "Audio", "Music", track + ".ogg")), track);
        }
    }

    [Fact]
    public void UniqueBosses_AreFlagged()
    {
        var db = TestContent.Content;
        string[] bosses = ["kobold_chief", "crypt_lich", "vault_warden", "stone_wyrm", "drowned_hydra", "sphinx", "sun_king", "ooze_mother", "wight_king", "umbral_wyrm", "rimefang", "choirmaster"];
        Assert.All(bosses, id => Assert.True(db.Monster(id).Boss, id));
        Assert.Equal(bosses.Order(), db.Monsters.Values.Where(m => m.Boss).Select(m => m.Id).Order());
    }

    [Theory]
    [InlineData("kobold", "a Kobold Sneak")]
    [InlineData("ice_beast", "an Ice Beast")]
    [InlineData("sun_king", "The Sun King")]
    public void MonsterNames_TakeTheRightArticle(string id, string expected) =>
        Assert.Equal(expected, TestContent.Content.Monster(id).NameWithArticle);

    [Fact]
    public void DetailedTextures_Are128Square_AndReplaceAClassicTexture()
    {
        var hd = Directory.GetFiles(Path.Combine(RepoAssets(), "Graphics", "TexturesHD"), "*.png");
        Assert.NotEmpty(hd);
        foreach (var f in hd)
        {
            Assert.True(File.Exists(Path.Combine(RepoAssets(), "Graphics", "Textures", Path.GetFileName(f))), Path.GetFileName(f));
            var head = File.ReadAllBytes(f).AsSpan(16, 8);
            var w = (head[0] << 24) | (head[1] << 16) | (head[2] << 8) | head[3];
            var h = (head[4] << 24) | (head[5] << 16) | (head[6] << 8) | head[7];
            Assert.Equal((128, 128), (w, h));
        }
    }
}
