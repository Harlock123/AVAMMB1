using AVAMMB1.Core.Persistence;
using AVAMMB1.Core.Rules;
using AVAMMB1.Core.Session;

namespace AVAMMB1.Tests;

public class SessionTests
{
    [Fact]
    public void NewGame_StartsInTownWithGold()
    {
        var s = TestContent.StartedSession();
        Assert.Equal("brindlemoor", s.State.MapId);
        Assert.Equal(6, s.State.Party.Count);
        Assert.Equal(720, s.State.Gold);
        Assert.True(s.State.IsExplored("brindlemoor", 16, 7, 14));
    }

    [Fact]
    public void Movement_TurnsAndBumpsIntoWalls()
    {
        var s = TestContent.StartedSession();
        Assert.Equal(Direction.North, s.State.Facing);
        var r = s.Move(MoveKind.Forward);
        Assert.True(r.Moved);
        Assert.Equal((7, 13), (s.State.X, s.State.Y));
        Assert.True(s.Move(MoveKind.Back).Moved);
        s.TurnLeft();
        Assert.Equal(Direction.West, s.State.Facing);
        s.TurnRight();
        s.TurnRight();
        Assert.Equal(Direction.East, s.State.Facing);
        // Walk east along the southern street until the town wall stops us.
        for (var i = 0; i < 20; i++)
        {
            s.Move(MoveKind.Forward);
        }
        Assert.Equal(14, s.State.X);
        var bump = s.Move(MoveKind.Forward);
        Assert.False(bump.Moved);
        Assert.Contains(bump.Messages, m => m.Sound == "bump");
    }

    [Fact]
    public void SteppingOnShop_OpensInteraction()
    {
        var s = TestContent.StartedSession();
        s.State.X = 11;
        s.State.Y = 8;
        s.State.Facing = Direction.North;
        var r = s.Move(MoveKind.Forward);
        Assert.NotNull(r.Interaction);
        Assert.Equal("brindle_smithy", r.Interaction!.Shop);
    }

    [Fact]
    public void Stairs_TeleportToDungeon()
    {
        var s = TestContent.StartedSession();
        s.State.X = 12;
        s.State.Y = 14;
        s.State.Facing = Direction.North;
        var r = s.Move(MoveKind.Forward);
        Assert.True(r.MapChanged);
        Assert.Equal("cellars", s.State.MapId);
        Assert.Equal(1, s.ViewDistance); // dark without light
        s.State.LightSteps = 10;
        Assert.True(s.ViewDistance > 1);
    }

    [Fact]
    public void Quest_RequiresItemAndSetsFlags()
    {
        var s = TestContent.StartedSession();
        s.State.X = 3;
        s.State.Y = 14;
        s.State.Facing = Direction.North;
        var r = s.Move(MoveKind.Forward);
        Assert.Contains("pell_met", s.State.Flags);
        Assert.NotNull(r.StoryText);
        // Bring the shard.
        s.State.Party[0].Backpack.Add(new AVAMMB1.Core.Items.ItemInstance("ember_shard"));
        s.Move(MoveKind.Back);
        var quest = s.Move(MoveKind.Forward);
        Assert.Contains("ember_given", s.State.Flags);
        Assert.DoesNotContain("Frost Shard waits", quest.StoryText); // quest text is shown alone
        Assert.Contains("Frost Shard waits", s.Interact().StoryText);
        Assert.True(AVAMMB1.Core.Items.Inventory.AnyoneHas(s.State.Party, "crypt_key"));
        Assert.False(AVAMMB1.Core.Items.Inventory.AnyoneHas(s.State.Party, "ember_shard"));
    }

    [Fact]
    public void BlockingEvent_PreventsEntry()
    {
        var s = TestContent.StartedSession();
        s.State.MapId = "wilds";
        s.State.X = 16;
        s.State.Y = 16;
        s.State.Facing = Direction.South;
        var r = s.Move(MoveKind.Forward);
        Assert.False(r.Moved);
        Assert.Equal("wilds", s.State.MapId);
    }

    [Fact]
    public void Rest_ConsumesFoodAndHeals()
    {
        var s = TestContent.StartedSession();
        var c = s.State.Party[0];
        c.Hp = 1;
        var food = c.Food;
        s.Rest(); // town: no encounters
        Assert.Equal(c.MaxHp, c.Hp);
        Assert.Equal(food - 1, c.Food);
        c.Food = 0;
        c.Hp = 1;
        s.Rest();
        Assert.Equal(1, c.Hp);
    }

    [Fact]
    public void SaveLoad_RoundTripsState()
    {
        var s = TestContent.StartedSession();
        s.Move(MoveKind.Forward);
        s.State.Flags.Add("test_flag");
        s.State.Party[1].Conditions = Condition.Poisoned | Condition.Blinded;
        s.State.Party[2].Backpack.Add(new AVAMMB1.Core.Items.ItemInstance("wand_lightning", 3));
        var dir = Path.Combine(Path.GetTempPath(), "avammb1-tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            var saves = new SaveGameService(dir);
            saves.Save(1, "Test", s.LocationSummary, s.State);
            Assert.True(saves.Exists(1));
            var list = saves.List();
            Assert.Equal(SaveGameService.TotalSlots, list.Count);
            Assert.Equal("Test", list[1].Name);
            Assert.Equal(1, saves.MostRecentSlot());

            var loaded = saves.Load(1).State;
            var s2 = TestContent.NewSession();
            s2.Load(loaded);
            Assert.Equal(s.State.X, s2.State.X);
            Assert.Equal(s.State.Y, s2.State.Y);
            Assert.Equal(s.State.Gold, s2.State.Gold);
            Assert.Contains("test_flag", s2.State.Flags);
            Assert.Equal(Condition.Poisoned | Condition.Blinded, s2.State.Party[1].Conditions);
            Assert.Equal(3, s2.State.Party[2].Backpack.Last().Charges);
            Assert.Equal(s.State.Party[0].Equipment[EquipSlot.Weapon].ItemId, s2.State.Party[0].Equipment[EquipSlot.Weapon].ItemId);
            Assert.Equal(s.State.Party[3].Stats[Stat.Personality], s2.State.Party[3].Stats[Stat.Personality]);
            Assert.Equal(s.State.Explored["brindlemoor"], s2.State.Explored["brindlemoor"]);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void CorruptSave_IsRejected()
    {
        Assert.Throws<InvalidDataException>(() => SaveGameService.Deserialize("{ not json"));
        Assert.Throws<InvalidDataException>(() => SaveGameService.Deserialize("{\"version\": 999}"));
    }

    [Fact]
    public void Settings_RoundTripAndNormalize()
    {
        var path = Path.Combine(Path.GetTempPath(), "avammb1-settings-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var store = new SettingsStore(path);
            var s = store.Load();
            Assert.Equal(60, s.MusicVolume);
            s.MusicVolume = 250;
            s.KeyBindings[InputAction.MoveForward] = ["I"];
            s.KeyBindings.Remove(InputAction.Rest);
            store.Save(s);
            var loaded = store.Load();
            Assert.Equal(100, loaded.MusicVolume);
            Assert.Equal(["I"], loaded.KeyBindings[InputAction.MoveForward]);
            Assert.Equal(["R"], loaded.KeyBindings[InputAction.Rest]);
            Assert.True(loaded.SmoothMovement);
            Assert.True(loaded.AnimateMonsters);
            loaded.SmoothMovement = false;
            loaded.AnimateMonsters = false;
            store.Save(loaded);
            Assert.False(store.Load().SmoothMovement);
            Assert.False(store.Load().AnimateMonsters);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void DataDirectory_CanBeOverridden()
    {
        var old = Environment.GetEnvironmentVariable(UserDataPaths.OverrideVariable);
        try
        {
            Environment.SetEnvironmentVariable(UserDataPaths.OverrideVariable, "/tmp/avammb1-x");
            Assert.Equal("/tmp/avammb1-x", UserDataPaths.DataDirectory);
            Environment.SetEnvironmentVariable(UserDataPaths.OverrideVariable, null);
            Assert.EndsWith("AVAMMB1", UserDataPaths.DataDirectory);
        }
        finally
        {
            Environment.SetEnvironmentVariable(UserDataPaths.OverrideVariable, old);
        }
    }
}
