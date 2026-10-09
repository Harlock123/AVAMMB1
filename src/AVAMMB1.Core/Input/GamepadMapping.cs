using AVAMMB1.Core.Persistence;

namespace AVAMMB1.Core.Input;

/// <summary>Controller buttons, using the common Xbox-style layout (A = bottom face button).</summary>
public enum GamepadButton
{
    /// <summary>D-pad or left stick up.</summary>
    Up,
    /// <summary>D-pad or left stick down.</summary>
    Down,
    /// <summary>D-pad or left stick left.</summary>
    Left,
    /// <summary>D-pad or left stick right.</summary>
    Right,
    /// <summary>Bottom face button (A / Cross).</summary>
    A,
    /// <summary>Right face button (B / Circle).</summary>
    B,
    /// <summary>Left face button (X / Square).</summary>
    X,
    /// <summary>Top face button (Y / Triangle).</summary>
    Y,
    /// <summary>Left shoulder.</summary>
    LeftShoulder,
    /// <summary>Right shoulder.</summary>
    RightShoulder,
    /// <summary>Left trigger.</summary>
    LeftTrigger,
    /// <summary>Right trigger.</summary>
    RightTrigger,
    /// <summary>Back / View / Select.</summary>
    Back,
    /// <summary>Start / Menu / Options.</summary>
    Start,
}

/// <summary>What the game is doing, which decides what each button means.</summary>
public enum GamepadContext
{
    /// <summary>Walking around with no dialog open.</summary>
    Exploring,
    /// <summary>Choosing an action in battle.</summary>
    Combat,
    /// <summary>Any menu, dialog or list (title screen, shops, spell lists...).</summary>
    Menu,
}

/// <summary>Combat commands a controller can issue.</summary>
public enum CombatCommand
{
    /// <summary>The obvious action: fight / attack (or shoot from the back rank) / continue.</summary>
    Primary,
    /// <summary>Open the spell list.</summary>
    Cast,
    /// <summary>Open the item list.</summary>
    UseItem,
    /// <summary>Block this round.</summary>
    Block,
    /// <summary>Try to run away.</summary>
    Run,
    /// <summary>Select the previous target.</summary>
    PreviousTarget,
    /// <summary>Select the next target.</summary>
    NextTarget,
    /// <summary>Repeat each character's last action for the rest of the round.</summary>
    Repeat,
    /// <summary>Start or stop auto-fight.</summary>
    AutoFight,
}

/// <summary>Menu navigation commands.</summary>
public enum MenuCommand
{
    /// <summary>Move focus up.</summary>
    Up,
    /// <summary>Move focus down.</summary>
    Down,
    /// <summary>Move focus left (or decrease a slider).</summary>
    Left,
    /// <summary>Move focus right (or increase a slider).</summary>
    Right,
    /// <summary>Press the focused control (or confirm the dialog).</summary>
    Activate,
    /// <summary>Close / cancel / go back.</summary>
    Back,
}

/// <summary>The result of mapping a button: at most one of the fields is set.</summary>
/// <param name="Action">Exploration action.</param>
/// <param name="Combat">Combat command.</param>
/// <param name="Menu">Menu command.</param>
public readonly record struct GamepadCommand(InputAction? Action = null, CombatCommand? Combat = null, MenuCommand? Menu = null)
{
    /// <summary>True when the button does nothing in this context.</summary>
    public bool IsNone => Action is null && Combat is null && Menu is null;
}

/// <summary>Maps controller buttons to game commands for each context.</summary>
public static class GamepadMapping
{
    /// <summary>Directional buttons repeat while held.</summary>
    /// <param name="button">Button.</param>
    public static bool Repeats(GamepadButton button) =>
        button is GamepadButton.Up or GamepadButton.Down or GamepadButton.Left or GamepadButton.Right;

    /// <summary>
    /// Default exploring buttons, one per action (Start always opens the menu as well, so a player can
    /// never lock themselves out). Players can rebind these in Settings.
    /// </summary>
    public static Dictionary<InputAction, GamepadButton> DefaultExploring() => new()
    {
        [InputAction.MoveForward] = GamepadButton.Up,
        [InputAction.MoveBack] = GamepadButton.Down,
        [InputAction.TurnLeft] = GamepadButton.Left,
        [InputAction.TurnRight] = GamepadButton.Right,
        [InputAction.StrafeLeft] = GamepadButton.LeftShoulder,
        [InputAction.StrafeRight] = GamepadButton.RightShoulder,
        [InputAction.Interact] = GamepadButton.A,
        [InputAction.Menu] = GamepadButton.B,
        [InputAction.Search] = GamepadButton.X,
        [InputAction.Characters] = GamepadButton.Y,
        [InputAction.Automap] = GamepadButton.Back,
        [InputAction.Rest] = GamepadButton.LeftTrigger,
        [InputAction.Cast] = GamepadButton.RightTrigger,
    };

    /// <summary>Rebinds an exploring action; another action using the same button loses it.</summary>
    /// <param name="bindings">Bindings to change.</param>
    /// <param name="action">Action.</param>
    /// <param name="button">New button.</param>
    /// <returns>The action that lost the button, if any.</returns>
    public static InputAction? Rebind(Dictionary<InputAction, GamepadButton> bindings, InputAction action, GamepadButton button)
    {
        InputAction? displaced = null;
        foreach (var (other, b) in bindings.ToList())
        {
            if (b == button && other != action)
            {
                bindings.Remove(other);
                displaced = other;
            }
        }
        bindings[action] = button;
        return displaced;
    }

    /// <summary>Maps a button press.</summary>
    /// <param name="context">Current context.</param>
    /// <param name="button">Button pressed.</param>
    /// <param name="exploring">The player's exploring bindings (defaults when null).</param>
    public static GamepadCommand Map(GamepadContext context, GamepadButton button, IReadOnlyDictionary<InputAction, GamepadButton>? exploring = null) => context switch
    {
        GamepadContext.Exploring => new GamepadCommand(Action: button == GamepadButton.Start
            ? InputAction.Menu
            : (exploring ?? DefaultExploring()).Where(kv => kv.Value == button).Select(kv => (InputAction?)kv.Key).FirstOrDefault()),
        GamepadContext.Combat => new GamepadCommand(Combat: button switch
        {
            GamepadButton.A => CombatCommand.Primary,
            GamepadButton.X => CombatCommand.Cast,
            GamepadButton.Y => CombatCommand.UseItem,
            GamepadButton.B => CombatCommand.Block,
            GamepadButton.Back => CombatCommand.Run,
            GamepadButton.LeftShoulder or GamepadButton.Left => CombatCommand.PreviousTarget,
            GamepadButton.RightShoulder or GamepadButton.Right => CombatCommand.NextTarget,
            GamepadButton.LeftTrigger => CombatCommand.Repeat,
            GamepadButton.RightTrigger => CombatCommand.AutoFight,
            _ => null,
        }),
        _ => new GamepadCommand(Menu: button switch
        {
            GamepadButton.Up => MenuCommand.Up,
            GamepadButton.Down => MenuCommand.Down,
            GamepadButton.Left or GamepadButton.LeftShoulder => MenuCommand.Left,
            GamepadButton.Right or GamepadButton.RightShoulder => MenuCommand.Right,
            GamepadButton.A or GamepadButton.Start => MenuCommand.Activate,
            GamepadButton.B or GamepadButton.Back => MenuCommand.Back,
            _ => null,
        }),
    };
}
