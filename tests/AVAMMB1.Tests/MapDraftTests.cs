using AVAMMB1.Core.Content;
using AVAMMB1.Core.Rules;
using AVAMMB1.Core.World;

namespace AVAMMB1.Tests;

/// <summary>The map editor's model.</summary>
public class MapDraftTests
{
    [Fact]
    public void EveryGameMap_SurvivesTheEditor_Unchanged()
    {
        foreach (var original in TestContent.Content.Maps.Values.Where(m => m.Id != Depths.MapId))
        {
            var draft = MapDraft.From(original.Def);
            var copy = GameMap.Parse(draft.ToMapDef());
            Assert.Equal((original.Width, original.Height), (copy.Width, copy.Height));
            for (var x = 0; x < original.Width; x++)
            {
                for (var y = 0; y < original.Height; y++)
                {
                    Assert.Equal(original.IsSolid(x, y), copy.IsSolid(x, y));
                    foreach (var d in new[] { Direction.North, Direction.East, Direction.South, Direction.West })
                    {
                        Assert.Equal(original.Probe(x, y, d).Wall, copy.Probe(x, y, d).Wall);
                    }
                }
            }
            Assert.Equal(original.Def.Events.Count, copy.Def.Events.Count);
            // And through the file format.
            var reread = MapDraft.FromJson(draft.ToJson()).ToMapDef();
            Assert.Equal(draft.ToMapDef().Grid, reread.Grid);
            static string Full(MapEventDef e) => System.Text.Json.JsonSerializer.Serialize(e, AVAMMB1.Core.Persistence.GameJsonContext.Default.MapEventDef);
            Assert.Equal(original.Def.Events.Select(Full), reread.Events.Select(Full)); // nothing lost by tidying
            Assert.Equal(System.Text.Json.JsonSerializer.Serialize(original.Def.Terrain, AVAMMB1.Core.Persistence.GameJsonContext.Default.DictionaryStringTerrainDef),
                System.Text.Json.JsonSerializer.Serialize(reread.Terrain, AVAMMB1.Core.Persistence.GameJsonContext.Default.DictionaryStringTerrainDef));
            Assert.Equal((original.Def.WallTexture, original.Def.Music, original.Def.EncounterChance, original.Def.Encounters.Count),
                (reread.WallTexture, reread.Music, reread.EncounterChance, reread.Encounters.Count));
        }
    }

    [Fact]
    public void ANewMap_IsWalledAllRound_AndEditsShareEdges()
    {
        var d = MapDraft.New("my_cave", "My Cave", MapKind.Dungeon, 4, 3);
        Assert.Equal('-', d.Edge(0, 0, CellSide.North));
        Assert.Equal('|', d.Edge(3, 2, CellSide.East));
        Assert.Equal(' ', d.Edge(1, 1, CellSide.East));

        d.SetEdge(1, 1, CellSide.East, 'D');
        Assert.Equal('D', d.Edge(2, 1, CellSide.West)); // the neighbour sees the same door
        d.SetEdge(0, 0, CellSide.North, ' ');
        Assert.Equal('-', d.Edge(0, 0, CellSide.North)); // the border stays walled
        d.SetEdge(0, 0, CellSide.West, 'D');
        Assert.Equal('D', d.Edge(0, 0, CellSide.West)); // but may have a door
        d.Box(3, 0, walled: true);
        Assert.Equal('-', d.Edge(3, 1, CellSide.North));

        var grid = d.ToMapDef().Grid;
        Assert.Equal(["+-+-+-+-+", "D. . .|.|", "+ + + +-+", "|. .D. .|", "+ + + + +", "|. . . .|", "+-+-+-+-+"], grid);
        Assert.Equal(WallKind.Door, GameMap.Parse(d.ToMapDef()).Probe(1, 1, Direction.East).Wall);
    }

    [Fact]
    public void Resizing_KeepsWhatFits()
    {
        var d = MapDraft.New("m", "M", MapKind.Dungeon, 5, 5);
        d.SetEdge(1, 1, CellSide.East, 'S');
        d.SetCell(2, 2, '#');
        d.Events.Add(new MapEventDef { X = 4, Y = 4, Type = MapEventKind.Treasure });
        d.Events.Add(new MapEventDef { X = 1, Y = 1, Type = MapEventKind.Message, Text = "Hello" });
        d.Resize(3, 8);
        Assert.Equal((3, 8), (d.Width, d.Height));
        Assert.Equal('S', d.Edge(1, 1, CellSide.East));
        Assert.Equal('#', d.Cell(2, 2));
        Assert.Equal('|', d.Edge(2, 0, CellSide.East)); // the new border
        Assert.Single(d.Events); // the treasure fell off the edge
        GameMap.Parse(d.ToMapDef());
    }

    [Fact]
    public void Problems_AreExplained()
    {
        var db = TestContent.Content;
        var d = MapDraft.New("my cave!", "My Cave", MapKind.Dungeon, 4, 4);
        d.Meta.Encounters.Add(new EncounterEntryDef { Monster = "dragon_of_nowhere" });
        d.Events.Add(new MapEventDef { X = 0, Y = 0, Type = MapEventKind.Teleport, Map = "brindlemoor", ToX = 7, ToY = 14 });
        d.Events.Add(new MapEventDef { X = 3, Y = 3, Type = MapEventKind.Shop, Shop = "no_such_shop" });
        d.Box(3, 3, walled: true);
        var problems = d.Problems(db);
        Assert.Contains(problems, p => p.Contains("map id", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.Contains("dragon_of_nowhere", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.Contains("no_such_shop", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.Contains("cannot be reached", StringComparison.Ordinal));

        var ok = MapDraft.New("my_cave", "My Cave", MapKind.Dungeon, 4, 4);
        ok.Events.Add(new MapEventDef { X = 0, Y = 0, Type = MapEventKind.Teleport, Map = "brindlemoor", ToX = 7, ToY = 14 });
        Assert.Empty(ok.Problems(db));
    }

    [Fact]
    public void ASavedMap_LoadsAsAModPack_WithItsWayIn()
    {
        var dir = Path.Combine(Path.GetTempPath(), "avammb1-editor-" + Guid.NewGuid().ToString("N"));
        try
        {
            var d = MapDraft.New("editor_cave", "Editor Cave", MapKind.Dungeon, 6, 5);
            d.SetEdge(2, 0, CellSide.East, 'W');
            d.Events.Add(new MapEventDef { X = 0, Y = 4, Type = MapEventKind.Teleport, Map = "brindlemoor", ToX = 7, ToY = 14, Facing = Direction.North });
            d.Events.Add(new MapEventDef { X = 5, Y = 0, Type = MapEventKind.Treasure, Gold = AVAMMB1.Core.Dice.DiceExpression.Parse("2d10"), Once = true });
            var folder = Path.Combine(dir, "my-maps");
            MapPackWriter.Save(d, folder, "my-maps", ("brindlemoor", 13, 4), (0, 4));
            MapPackWriter.Save(d, folder, "my-maps", ("brindlemoor", 12, 4), (0, 4)); // saving again moves the way in

            var problems = new List<string>();
            var packs = ModCatalog.Discover([dir], problems);
            Assert.Empty(problems);
            var db = ContentDatabase.Load(new EmbeddedContentSource(), packs);
            Assert.Equal(d.ToMapDef().Grid, db.Map("editor_cave").Def.Grid);
            var ways = db.Map("brindlemoor").Def.Events.Where(e => e.Map == "editor_cave").ToList();
            var way = Assert.Single(ways);
            Assert.Equal((12, 4, 0, 4), (way.X, way.Y, way.ToX, way.ToY));
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public void ATestPlay_UsesTheEditedMap_ThenPutsTheGameBack()
    {
        // Its own copy of the content: the shared one is read by other tests at the same time.
        var s = new AVAMMB1.Core.Session.GameSession(ContentDatabase.Load(new EmbeddedContentSource()), new AVAMMB1.Core.Dice.DefaultRandomSource(1));
        var original = s.Content.Map("cellars");
        var d = MapDraft.From(original.Def);
        d.SetCell(1, 1, '#');
        var old = s.Content.UseEditedMap(d.ToMapDef());
        try
        {
            s.StartTestPlay("cellars", s.Content.Config.Premades.Select(s.Factory.CreatePremade), 0, 0, level: 4);
            Assert.True(s.TestPlaying);
            Assert.True(s.CurrentMap.IsSolid(1, 1));
            Assert.All(s.State.Party, c => Assert.Equal(4, c.Level));
        }
        finally
        {
            s.EndTestPlay();
            s.Content.RestoreMap("cellars", old);
        }
        Assert.False(s.TestPlaying);
        Assert.Same(original, s.Content.Map("cellars"));
    }
}
