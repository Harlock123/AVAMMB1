using AVAMMB1.Core.Input;
using AVAMMB1.Core.Persistence;

namespace AVAMMB1.Tests;

public class GamepadMappingTests
{
    [Theory]
    [InlineData(GamepadButton.Up, InputAction.MoveForward)]
    [InlineData(GamepadButton.Down, InputAction.MoveBack)]
    [InlineData(GamepadButton.Left, InputAction.TurnLeft)]
    [InlineData(GamepadButton.Right, InputAction.TurnRight)]
    [InlineData(GamepadButton.LeftShoulder, InputAction.StrafeLeft)]
    [InlineData(GamepadButton.RightShoulder, InputAction.StrafeRight)]
    [InlineData(GamepadButton.A, InputAction.Interact)]
    [InlineData(GamepadButton.X, InputAction.Search)]
    [InlineData(GamepadButton.Y, InputAction.Characters)]
    [InlineData(GamepadButton.Back, InputAction.Automap)]
    [InlineData(GamepadButton.Start, InputAction.Menu)]
    [InlineData(GamepadButton.LeftTrigger, InputAction.Rest)]
    [InlineData(GamepadButton.RightTrigger, InputAction.Cast)]
    public void Exploring_MapsToGameActions(GamepadButton button, InputAction expected) =>
        Assert.Equal(expected, GamepadMapping.Map(GamepadContext.Exploring, button).Action);

    [Theory]
    [InlineData(GamepadButton.A, CombatCommand.Primary)]
    [InlineData(GamepadButton.X, CombatCommand.Cast)]
    [InlineData(GamepadButton.Y, CombatCommand.UseItem)]
    [InlineData(GamepadButton.B, CombatCommand.Block)]
    [InlineData(GamepadButton.LeftShoulder, CombatCommand.PreviousTarget)]
    [InlineData(GamepadButton.RightShoulder, CombatCommand.NextTarget)]
    public void Combat_MapsToCombatCommands(GamepadButton button, CombatCommand expected) =>
        Assert.Equal(expected, GamepadMapping.Map(GamepadContext.Combat, button).Combat);

    [Theory]
    [InlineData(GamepadButton.Up, MenuCommand.Up)]
    [InlineData(GamepadButton.Down, MenuCommand.Down)]
    [InlineData(GamepadButton.A, MenuCommand.Activate)]
    [InlineData(GamepadButton.B, MenuCommand.Back)]
    public void Menus_MapToNavigation(GamepadButton button, MenuCommand expected) =>
        Assert.Equal(expected, GamepadMapping.Map(GamepadContext.Menu, button).Menu);

    [Fact]
    public void EveryButtonDoesSomethingWhileExploring()
    {
        foreach (var b in Enum.GetValues<GamepadButton>().Where(b => b != GamepadButton.B))
        {
            Assert.False(GamepadMapping.Map(GamepadContext.Exploring, b).IsNone, b.ToString());
        }
    }

    [Fact]
    public void OnlyDirectionsRepeat()
    {
        Assert.True(GamepadMapping.Repeats(GamepadButton.Up));
        Assert.False(GamepadMapping.Repeats(GamepadButton.A));
    }
}
