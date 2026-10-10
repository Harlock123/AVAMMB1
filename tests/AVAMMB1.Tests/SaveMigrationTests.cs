using System.Text.Json.Nodes;
using AVAMMB1.Core.Persistence;
using AVAMMB1.Core.Rules;

namespace AVAMMB1.Tests;

/// <summary>
/// Saves written by every released version must keep loading. The fixtures are genuine save files
/// written by the game code of each release tag (<c>tools/make-save-fixture.sh</c>), all with the same
/// party and progress.
/// </summary>
public class SaveMigrationTests
{
    private static string FixtureDir => Path.Combine(AppContext.BaseDirectory, "Fixtures", "Saves");

    /// <summary>Every release with a fixture.</summary>
    public static TheoryData<string> Releases => new(Directory.GetFiles(FixtureDir, "v*.json").Select(Path.GetFileNameWithoutExtension).OfType<string>().Order());

    /// <summary>Releases 1.0-1.4, which wrote the first save format.</summary>
    public static TheoryData<string> FirstFormatReleases => new() { "v1.0.0", "v1.1.0", "v1.2.0", "v1.3.0", "v1.4.0" };

    private static string Fixture(string release) => File.ReadAllText(Path.Combine(FixtureDir, release + ".json"));

    [Fact]
    public void EveryReleaseInTheChangelog_HasASaveFixture()
    {
        var changelog = File.ReadAllText(Path.Combine(TestPaths.RepoRoot, "CHANGELOG.md"));
        var released = System.Text.RegularExpressions.Regex.Matches(changelog, @"^## \[(\d+\.\d+\.\d+)\]", System.Text.RegularExpressions.RegexOptions.Multiline)
            .Select(m => "v" + m.Groups[1].Value).ToList();
        Assert.True(released.Count >= 15);
        var missing = released.Where(r => !File.Exists(Path.Combine(FixtureDir, r + ".json"))).ToList();
        Assert.True(missing.Count == 0, $"No save fixture for {string.Join(", ", missing)} - run tools/make-save-fixture.sh <tag>");
    }

    [Theory]
    [MemberData(nameof(Releases))]
    public void ReleasedSaves_SayWhichGameWroteThem(string release)
    {
        var node = JsonNode.Parse(Fixture(release))!;
        if (SaveMigrations.VersionOf((JsonObject)node) >= 2)
        {
            Assert.Equal(release[1..], node["gameVersion"]!.GetValue<string>());
        }
    }

    [Theory]
    [MemberData(nameof(Releases))]
    public void ReleasedSaves_SurviveARoundTripInTheCurrentGame(string release)
    {
        var s = TestContent.NewSession();
        s.Load(SaveGameService.Deserialize(Fixture(release)).State);
        s.Rest();
        var json = SaveGameService.Serialize(new SaveFile { State = s.State });
        var again = SaveGameService.Deserialize(json).State;
        Assert.Equal(json, SaveGameService.Serialize(new SaveFile { State = again }));
        Assert.Equal(s.State.Party.Select(c => c.Hp), again.Party.Select(c => c.Hp));
    }

    [Theory]
    [MemberData(nameof(Releases))]
    public void ReleasedSaves_LoadIntoTheCurrentGame(string release)
    {
        var file = SaveGameService.Deserialize(Fixture(release));
        Assert.Equal(SaveFile.CurrentVersion, file.Version);
        var state = file.State;
        Assert.Equal("brindlemoor", state.MapId);
        Assert.Equal((6, 13), (state.X, state.Y));
        Assert.Equal(6, state.Party.Count);
        Assert.Equal(700, state.Gold); // the purse (all gold was in the purse before 1.4)
        Assert.Contains("pell_met", state.Flags);
        Assert.Equal(300, state.Party[0].Experience);
        Assert.Equal(3, state.Party[3].Hp);
        Assert.Contains(state.Party[2].Backpack, i => i.ItemId == "potion_healing");
        Assert.All(state.Party, c => Assert.NotNull(c.LearnedSpells));

        // And the session accepts it and can keep playing.
        var s = TestContent.NewSession();
        s.Load(state);
        Assert.True(s.Move(AVAMMB1.Core.Session.MoveKind.Back).Moved || s.Move(AVAMMB1.Core.Session.MoveKind.Forward).Moved);
        Assert.Equal(3, s.State.Party[3].Hp);
    }

    [Theory]
    [MemberData(nameof(FirstFormatReleases))]
    public void Upgrading_DropsComputedValues_AndAddsNewFields(string release)
    {
        var node = (JsonObject)JsonNode.Parse(Fixture(release))!;
        Assert.Equal(1, SaveMigrations.Upgrade(node));
        var state = (JsonObject)node["state"]!;
        Assert.False(state.ContainsKey("day"));
        Assert.False(state.ContainsKey("totalGold"));
        Assert.True(state.ContainsKey("foundSecrets"));
        var member = (JsonObject)((JsonArray)state["party"]!)[0]!;
        Assert.False(member.ContainsKey("isAlive"));
        Assert.True(member.ContainsKey("gold"));
        Assert.Equal(SaveFile.CurrentVersion, SaveMigrations.VersionOf(node));
    }

    [Fact]
    public void CurrentSaves_DontStoreComputedValues()
    {
        var s = TestContent.StartedSession();
        var json = SaveGameService.Serialize(new SaveFile { State = s.State });
        var node = (JsonObject)JsonNode.Parse(json)!;
        Assert.Equal(SaveFile.CurrentVersion, SaveMigrations.VersionOf(node));
        Assert.False(((JsonObject)node["state"]!).ContainsKey("totalGold"));
        Assert.False(((JsonObject)((JsonArray)node["state"]!["party"]!)[0]!).ContainsKey("canAct"));
        Assert.False(string.IsNullOrEmpty(node["gameVersion"]?.GetValue<string>()));
    }

    [Fact]
    public void SavesFromANewerGame_AreRefusedWithAClearMessage()
    {
        var node = (JsonObject)JsonNode.Parse(Fixture("v1.4.0"))!;
        node["version"] = SaveFile.CurrentVersion + 1;
        node["gameVersion"] = "9.9.9";
        var ex = Assert.Throws<InvalidDataException>(() => SaveGameService.Deserialize(node.ToJsonString()));
        Assert.Contains("newer version", ex.Message);
        Assert.Contains("9.9.9", ex.Message);
    }

    [Fact]
    public void OverwritingAnOldSave_KeepsABackup()
    {
        var dir = Path.Combine(Path.GetTempPath(), "avammb1-migrate-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "slot1.json"), Fixture("v1.0.0"));
            var saves = new SaveGameService(dir);
            var loaded = saves.Load(1);
            saves.Save(1, "Upgraded", "x", loaded.State);
            Assert.True(File.Exists(Path.Combine(dir, "slot1.json.v1.bak")));
            Assert.Equal(SaveFile.CurrentVersion, saves.Load(1).Version);
            Assert.Equal(Fixture("v1.0.0"), File.ReadAllText(Path.Combine(dir, "slot1.json.v1.bak")));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
