using System.Runtime.InteropServices;
using Avalonia.Threading;
using AVAMMB1.Core.Input;
using Silk.NET.Core.Contexts;
using Silk.NET.SDL;
using Thread = System.Threading.Thread;

namespace AVAMMB1.App.Services;

/// <summary>
/// Reads game controllers through SDL2's GameController API (Xbox, PlayStation, Switch Pro and the
/// many pads SDL knows). Polling runs on a background thread so SDL never touches the UI thread's
/// message loop; button presses are posted to the UI thread. Directional input repeats while held.
/// </summary>
public sealed unsafe class GamepadService : IDisposable
{
    private const short StickThreshold = 16000;
    private const short TriggerThreshold = 12000;
    private static readonly TimeSpan RepeatDelay = TimeSpan.FromMilliseconds(380);
    private static readonly TimeSpan RepeatRate = TimeSpan.FromMilliseconds(190);

    private readonly Thread? _thread;
    private readonly HashSet<GamepadButton> _held = new();
    private readonly Dictionary<GamepadButton, DateTime> _nextRepeat = new();
    private volatile bool _stop;
    private int _connected;

    private GamepadService(bool start)
    {
        if (start)
        {
            _thread = new Thread(Run) { IsBackground = true, Name = "AVAMMB1 gamepad" };
            _thread.Start();
        }
    }

    /// <summary>Raised on the UI thread for each press (and each repeat of a held direction).</summary>
    public event Action<GamepadButton>? ButtonPressed;

    /// <summary>Human readable status for the settings screen.</summary>
    public string Status { get; private set; } = "Gamepad: starting...";

    /// <summary>Number of connected controllers.</summary>
    public int Connected => _connected;

    /// <summary>Starts the service (or returns an idle one when disabled).</summary>
    /// <param name="enabled">Whether to look for controllers at all.</param>
    public static GamepadService Create(bool enabled) => new(enabled) { Status = enabled ? "Gamepad: starting..." : "Gamepad support disabled" };

    /// <summary>
    /// Loads the SDL2 library bundled with the game (via .NET's native probing, which knows where
    /// single-file builds extract native libraries), falling back to a system SDL2.
    /// </summary>
    /// <param name="detail">Which library was loaded.</param>
    public static Sdl LoadApi(out string detail)
    {
        string bundled = OperatingSystem.IsWindows() ? "SDL2.dll" : OperatingSystem.IsMacOS() ? "libSDL2-2.0.dylib" : "libSDL2-2.0.so";
        if (NativeLibrary.TryLoad(bundled, typeof(GamepadService).Assembly, DllImportSearchPath.AssemblyDirectory, out var handle))
        {
            detail = $"bundled SDL2 ({bundled})";
            return new Sdl(new LamdaNativeContext(name => NativeLibrary.TryGetExport(handle, name, out var p) ? p : 0));
        }
        detail = "system SDL2";
        return Sdl.GetApi();
    }

    /// <summary>Checks that the SDL library loads and initialises (used by <c>--smoke-test</c>).</summary>
    /// <returns>A short description; starts with "bundled" when the bundled library was used.</returns>
    public static string Probe()
    {
        var sdl = LoadApi(out var lib);
        sdl.SetHint(Sdl.HintNoSignalHandlers, "1");
        if (sdl.Init(Sdl.InitGamecontroller | Sdl.InitEvents) != 0)
        {
            return $"{lib} loaded but could not initialise controllers: " + sdl.GetErrorS();
        }
        var count = sdl.NumJoysticks();
        sdl.QuitSubSystem(Sdl.InitGamecontroller | Sdl.InitEvents);
        return $"{lib} ok, {count} controller(s) connected";
    }

    private void Run()
    {
        Sdl sdl;
        try
        {
            sdl = LoadApi(out _);
            sdl.SetHint(Sdl.HintNoSignalHandlers, "1");
            sdl.SetHint(Sdl.HintJoystickAllowBackgroundEvents, "1");
            if (sdl.Init(Sdl.InitGamecontroller | Sdl.InitEvents) != 0)
            {
                Status = "Gamepad unavailable: " + sdl.GetErrorS();
                return;
            }
        }
        catch (Exception ex)
        {
            Status = "Gamepad unavailable: " + ex.Message;
            return;
        }
        Status = "Gamepad ready - no controller connected";
        var axes = new Dictionary<GameControllerAxis, short>();
        try
        {
            while (!_stop)
            {
                Event e;
                while (sdl.PollEvent(&e) != 0)
                {
                    switch ((EventType)e.Type)
                    {
                        case EventType.Controllerdeviceadded:
                            if (sdl.GameControllerOpen(e.Cdevice.Which) != null)
                            {
                                _connected++;
                                Status = $"Gamepad: {_connected} controller(s) connected";
                            }
                            break;
                        case EventType.Controllerdeviceremoved:
                            _connected = Math.Max(0, _connected - 1);
                            Status = _connected == 0 ? "Gamepad ready - no controller connected" : $"Gamepad: {_connected} controller(s) connected";
                            break;
                        case EventType.Controllerbuttondown:
                            if (MapButton((GameControllerButton)e.Cbutton.Button) is { } down)
                            {
                                Press(down);
                            }
                            break;
                        case EventType.Controllerbuttonup:
                            if (MapButton((GameControllerButton)e.Cbutton.Button) is { } up)
                            {
                                Release(up);
                            }
                            break;
                        case EventType.Controlleraxismotion:
                            axes[(GameControllerAxis)e.Caxis.Axis] = e.Caxis.Value;
                            UpdateAxes(axes);
                            break;
                    }
                }
                Repeat();
                Thread.Sleep(10);
            }
        }
        finally
        {
            sdl.Quit();
        }
    }

    private static GamepadButton? MapButton(GameControllerButton b) => b switch
    {
        GameControllerButton.DpadUp => GamepadButton.Up,
        GameControllerButton.DpadDown => GamepadButton.Down,
        GameControllerButton.DpadLeft => GamepadButton.Left,
        GameControllerButton.DpadRight => GamepadButton.Right,
        GameControllerButton.A => GamepadButton.A,
        GameControllerButton.B => GamepadButton.B,
        GameControllerButton.X => GamepadButton.X,
        GameControllerButton.Y => GamepadButton.Y,
        GameControllerButton.Leftshoulder => GamepadButton.LeftShoulder,
        GameControllerButton.Rightshoulder => GamepadButton.RightShoulder,
        GameControllerButton.Back => GamepadButton.Back,
        GameControllerButton.Start => GamepadButton.Start,
        _ => null,
    };

    /// <summary>Turns the left stick into D-pad presses and the triggers into buttons.</summary>
    private void UpdateAxes(Dictionary<GameControllerAxis, short> axes)
    {
        short Get(GameControllerAxis a) => axes.TryGetValue(a, out var v) ? v : (short)0;
        var x = Get(GameControllerAxis.Leftx);
        var y = Get(GameControllerAxis.Lefty);
        Set(GamepadButton.Left, x < -StickThreshold);
        Set(GamepadButton.Right, x > StickThreshold);
        Set(GamepadButton.Up, y < -StickThreshold);
        Set(GamepadButton.Down, y > StickThreshold);
        Set(GamepadButton.LeftTrigger, Get(GameControllerAxis.Triggerleft) > TriggerThreshold);
        Set(GamepadButton.RightTrigger, Get(GameControllerAxis.Triggerright) > TriggerThreshold);
    }

    private void Set(GamepadButton b, bool down)
    {
        if (down && !_held.Contains(b))
        {
            Press(b);
        }
        else if (!down && _held.Contains(b))
        {
            Release(b);
        }
    }

    private void Press(GamepadButton b)
    {
        _held.Add(b);
        if (GamepadMapping.Repeats(b))
        {
            _nextRepeat[b] = DateTime.UtcNow + RepeatDelay;
        }
        Raise(b);
    }

    private void Release(GamepadButton b)
    {
        _held.Remove(b);
        _nextRepeat.Remove(b);
    }

    private void Repeat()
    {
        var now = DateTime.UtcNow;
        foreach (var (b, due) in _nextRepeat.ToList())
        {
            if (now >= due)
            {
                _nextRepeat[b] = now + RepeatRate;
                Raise(b);
            }
        }
    }

    private void Raise(GamepadButton b) => Dispatcher.UIThread.Post(() => ButtonPressed?.Invoke(b));

    /// <inheritdoc />
    public void Dispose()
    {
        _stop = true;
        _thread?.Join(500);
    }
}
