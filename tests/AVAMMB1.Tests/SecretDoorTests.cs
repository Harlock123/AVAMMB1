using AVAMMB1.Core.Content;
using AVAMMB1.Core.Persistence;
using AVAMMB1.Core.Rules;
using AVAMMB1.Core.Session;
using AVAMMB1.Core.World;

namespace AVAMMB1.Tests;

public class SecretDoorTests
{
    // Cellars: the smuggler's cache at (3,3) is reached from (4,3) through a secret door to the west.
    private static GameSession AtDraftyWall(int seed = 7)
    {
        var s = TestContent.StartedSession(seed);
        s.State.MapId = "cellars";
        s.State.X = 4;
        s.State.Y = 3;
        s.State.Facing = Direction.West;
        s.Explore();
        return s;
    }

    [Fact]
    public void Content_HasHiddenRoomsReachableOnlyThroughSecretDoors()
    {
        foreach (var (mapId, x, y) in new[] { ("cellars", 3, 3), ("crypt", 7, 5) })
        {
            var map = TestContent.Content.Map(mapId);
            var sides = Enum.GetValues<Direction>().Where(d => map.GetWall(x, y, d) != WallKind.Wall).ToList();
            Assert.Equal([WallKind.SecretDoor], sides.Select(d => map.GetWall(x, y, d)));
            Assert.Contains(map.EventsAt(x, y), e => e.Type == MapEventKind.Treasure);
        }
    }

    [Fact]
    public void UndiscoveredSecretDoor_BlocksLikeAWall_AndStaysOffTheAutomap()
    {
        var s = AtDraftyWall();
        Assert.Equal(WallKind.SecretDoor, s.CurrentMap.GetWall(4, 3, Direction.West));
        Assert.False(s.CanPass(4, 3, Direction.West));
        var r = s.Move(MoveKind.Forward);
        Assert.False(r.Moved);
        Assert.Contains(r.Messages, m => m.Sound == "bump");
        Assert.False(s.State.IsExplored("cellars", 16, 3, 3));
    }

    [Fact]
    public void Search_FindsTheDoor_ThenThePartyCanWalkThrough()
    {
        var s = AtDraftyWall();
        var stepsBefore = s.State.Steps;
        StepResult r;
        var tries = 0;
        do
        {
            r = s.Search();
            if (s.Combat is not null)
            {
                s.Combat.TryBribe(); // get rid of wandering monsters quickly; outcome irrelevant here
                s.EndCombat();
            }
        }
        while (!s.CanPass(4, 3, Direction.West) && ++tries < 50);

        Assert.True(s.CanPass(4, 3, Direction.West));
        Assert.True(s.State.Steps >= stepsBefore + 5);
        Assert.True(s.State.IsSecretFound("cellars", 3, 3, Direction.East)); // same edge seen from the other side

        var move = s.Move(MoveKind.Forward);
        Assert.True(move.Moved);
        Assert.Equal((3, 3), (s.State.X, s.State.Y));
        Assert.Contains(move.Messages, m => m.Text.Contains("Smuggler", StringComparison.Ordinal) || m.Kind == MessageKind.Loot);
    }

    [Fact]
    public void Search_ReportsSuccessWithAScriptedRoll()
    {
        var s = new GameSession(TestContent.Content, new ScriptedRandom(0)); // every percentile roll succeeds
        s.NewGame(TestContent.Content.Config.Premades.Select(s.Factory.CreatePremade));
        s.State.MapId = "cellars";
        s.State.X = 4;
        s.State.Y = 3;
        s.State.Facing = Direction.North;
        var r = s.Search();
        Assert.Contains(r.Messages, m => m.Text.Contains("secret door to the left", StringComparison.Ordinal));
    }

    [Fact]
    public void Search_WithNothingHidden_FindsNothing()
    {
        var s = TestContent.StartedSession();
        var r = s.Search();
        Assert.Contains(r.Messages, m => m.Text == "You find nothing unusual.");
        Assert.Empty(s.State.FoundSecrets);
    }

    [Fact]
    public void SearchChance_FavorsCleverLuckyRobbers()
    {
        var s = TestContent.NewSession();
        var robber = TestContent.Make(s, "robber", stats: 18);
        var knight = TestContent.Make(s, "knight", stats: 15);
        Assert.True(s.SearchChance(robber) > s.SearchChance(knight));
        Assert.InRange(s.SearchChance(knight), 20, 95);
    }

    [Fact]
    public void FoundSecrets_SurviveSaveAndLoad()
    {
        var s = AtDraftyWall();
        s.State.FoundSecrets.Add(GameState.SecretKey("cellars", 4, 3, Direction.West));
        var json = SaveGameService.Serialize(new SaveFile { State = s.State });
        var s2 = TestContent.NewSession();
        s2.Load(SaveGameService.Deserialize(json).State);
        Assert.True(s2.CanPass(4, 3, Direction.West));
    }

    [Fact]
    public void SearchKey_IsBoundByDefault_AndAddedToOldSettings()
    {
        Assert.Equal(["F"], GameSettings.DefaultBindings()[InputAction.Search]);
        var old = new GameSettings();
        old.KeyBindings.Remove(InputAction.Search);
        old.Normalize();
        Assert.Equal(["F"], old.KeyBindings[InputAction.Search]);
    }
}
