using AVAMMB1.Core.Content;
using AVAMMB1.Core.Dice;
using AVAMMB1.Core.Items;
using AVAMMB1.Core.Session;

namespace AVAMMB1.Tests;

/// <summary>Mod packs: discovery, merging by id, map patches, errors, and the shipped example pack.</summary>
public class ModPackTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "avammb1-mods-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private static string ExamplePack()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AVAMMB1.sln")))
        {
            dir = dir.Parent;
        }
        return Path.Combine(dir!.FullName, "docs", "modding", "example-pack");
    }

    private string Pack(string folder, string manifest, params (string File, string Json)[] files)
    {
        var root = Path.Combine(_dir, folder);
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "pack.json"), manifest);
        foreach (var (file, json) in files)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(root, file))!);
            File.WriteAllText(Path.Combine(root, file), json);
        }
        return root;
    }

    [Fact]
    public void ExamplePack_LoadsValidates_AndLinksIntoBrindlemoor()
    {
        var problems = new List<string>();
        var packs = ModCatalog.Discover([Path.GetDirectoryName(ExamplePack())!], problems);
        Assert.Empty(problems);
        var pack = Assert.Single(packs);
        Assert.Equal("example-old-well", pack.Id);

        var db = ContentDatabase.Load(new EmbeddedContentSource(), packs);
        Assert.Empty(db.FindProblems());
        Assert.Equal("Well Crab", db.Monster("well_crab").Name);
        Assert.Contains(db.Quests, q => q.Id == "oldwell");
        Assert.Contains(db.Map("brindlemoor").AllEvents, e => e.Map == "oldwell");
        var well = db.Map("oldwell");
        var entry = well.AllEvents.First(e => e.Type == MapEventKind.Teleport);
        Assert.All(well.AllEvents, e => Assert.Contains((e.X, e.Y), well.Reachable(entry.X, entry.Y)));

        // Walk down the well, beat the crabs, take the charm.
        var s = new GameSession(db, new DefaultRandomSource(5));
        s.NewGame(db.Config.Premades.Select(s.Factory.CreatePremade));
        foreach (var c in s.State.Party) { c.Level = 10; c.MaxHp = c.Hp = 200; }
        var w = new Walker(s);
        w.Travel("oldwell");
        w.GoTo(e => e.Id == "oldwell_crabs");
        w.GoTo(e => e.Id == "oldwell_wishes");
        Assert.True(Inventory.AnyoneHas(s.State.Party, "well_charm"));
        Assert.Contains(QuestJournal.Quests(s.State, db), q => q.Id == "oldwell" && q.Done);

        // The base game is untouched without the pack.
        Assert.DoesNotContain(TestContent.Content.Map("brindlemoor").AllEvents, e => e.Map == "oldwell");
    }

    [Fact]
    public void Packs_OverrideByIdInFolderOrder_AndBrokenManifestsAreReported()
    {
        Pack("a-first", """{ "id": "first", "name": "First" }""",
            ("monsters.json", """[ { "id": "kobold", "name": "Kobold Champion", "sprite": "kobold", "level": 3, "hitPoints": "20", "xp": 50, "attacks": [ { "verb": "stabs", "damage": "1d8" } ] } ]"""));
        Pack("b-second", """{ "id": "second" }""",
            ("monsters.json", """[ { "id": "kobold", "name": "Kobold King", "sprite": "kobold", "level": 4, "hitPoints": "30", "xp": 80, "attacks": [ { "verb": "stabs", "damage": "1d8" } ] } ]"""));
        Pack("c-broken", "{ not json");
        Pack("d-dup", """{ "id": "first" }""");
        Directory.CreateDirectory(Path.Combine(_dir, "e-no-manifest"));

        var problems = new List<string>();
        var packs = ModCatalog.Discover([_dir, Path.Combine(_dir, "missing")], problems);
        Assert.Equal(["first", "second"], packs.Select(p => p.Id));
        Assert.Equal("second", packs[1].Manifest.Name); // name defaults to the id
        Assert.Equal(2, problems.Count);

        var db = ContentDatabase.Load(new EmbeddedContentSource(), packs);
        Assert.Equal("Kobold King", db.Monster("kobold").Name);
        Assert.Equal(TestContent.Content.Monsters.Count, db.Monsters.Count);
    }

    [Fact]
    public void BadPackContent_NamesThePack()
    {
        Pack("bad", """{ "id": "bad", "name": "Bad Pack" }""", ("items.json", "[ { oops"));
        var packs = ModCatalog.Discover([_dir], []);
        var ex = Assert.Throws<InvalidDataException>(() => ContentDatabase.Load(new EmbeddedContentSource(), packs));
        Assert.Contains("Bad Pack", ex.Message);

        Directory.Delete(_dir, recursive: true);
        Pack("patch", """{ "id": "patch", "name": "Patch Pack" }""", ("mapPatches.json", """[ { "map": "nowhere", "addEvents": [] } ]"""));
        ex = Assert.Throws<InvalidDataException>(() => ContentDatabase.Load(new EmbeddedContentSource(), ModCatalog.Discover([_dir], [])));
        Assert.Contains("Patch Pack", ex.Message);
        Assert.Contains("nowhere", ex.Message);

        Directory.Delete(_dir, recursive: true);
        Pack("ref", """{ "id": "ref", "name": "Ref Pack" }""", ("shops.json", """[ { "id": "s", "name": "S", "stock": [ "no_such_item" ] } ]"""));
        ex = Assert.Throws<InvalidDataException>(() => ContentDatabase.Load(new EmbeddedContentSource(), ModCatalog.Discover([_dir], [])));
        Assert.Contains("no_such_item", ex.Message);
        Assert.Contains("Ref Pack", ex.Message);
    }
}
