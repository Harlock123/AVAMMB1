using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Input;
using Avalonia.Platform;
using AVAMMB1.Core.Persistence;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AVAMMB1.App.ViewModels;

/// <summary>A save slot row.</summary>
/// <param name="Info">Slot info.</param>
public sealed record SlotRow(SaveSlotInfo Info)
{
    /// <summary>Slot label.</summary>
    public string Label => Info.Slot == 0 ? "Quick" : $"Slot {Info.Slot}";
    /// <summary>Description.</summary>
    public string Description => Info.Exists
        ? $"{Info.Name} - {Info.Summary} - {Info.SavedUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)}"
        : "(empty)";
}

/// <summary>Save / load slot picker.</summary>
public sealed partial class SaveLoadViewModel : ViewModelBase
{
    private readonly MainViewModel _main;
    private readonly Action _onClose;

    /// <summary>Creates the picker.</summary>
    /// <param name="main">Root view model.</param>
    /// <param name="saving">True to save, false to load.</param>
    /// <param name="onClose">Called when backing out.</param>
    public SaveLoadViewModel(MainViewModel main, bool saving, Action onClose)
    {
        _main = main;
        Saving = saving;
        _onClose = onClose;
        Reload();
        Feedback = saving ? "Choose a slot to save into." : "Choose a saved game to load.";
        Location = main.Services.Saves.Directory;
    }

    /// <summary>Whether saving.</summary>
    public bool Saving { get; }
    /// <summary>Title.</summary>
    public string Title => Saving ? "Save Game" : "Load Game";
    /// <summary>Save directory.</summary>
    public string Location { get; }
    /// <summary>Slots.</summary>
    public ObservableCollection<SlotRow> Slots { get; } = new();

    /// <summary>Feedback.</summary>
    [ObservableProperty]
    private string _feedback = "";

    private void Reload()
    {
        Slots.Clear();
        foreach (var s in _main.Services.Saves.List())
        {
            Slots.Add(new SlotRow(s));
        }
    }

    [RelayCommand]
    private void Pick(SlotRow row)
    {
        if (Saving)
        {
            var session = _main.Services.Session;
            try
            {
                var name = $"{session.State.Party.FirstOrDefault()?.Name ?? "Party"}'s party";
                _main.Services.Saves.Save(row.Info.Slot, name, session.LocationSummary, session.State);
                _main.Services.Audio.PlaySfx("book");
                Feedback = $"Saved to {row.Label}.";
                Reload();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Feedback = "Save failed: " + ex.Message;
            }
            return;
        }
        if (!row.Info.Exists)
        {
            Feedback = "That slot is empty.";
            return;
        }
        var error = _main.LoadSlot(row.Info.Slot);
        if (error is not null)
        {
            Feedback = "Load failed: " + error;
        }
    }

    [RelayCommand]
    private void Back() => _onClose();

    /// <inheritdoc />
    public override bool HandleKey(Key key)
    {
        if (key == Key.Escape)
        {
            Back();
            return true;
        }
        return false;
    }
}

/// <summary>A key binding row.</summary>
public sealed partial class BindingRow : ObservableObject
{
    /// <summary>Creates the row.</summary>
    /// <param name="action">Action.</param>
    /// <param name="keys">Bound keys.</param>
    public BindingRow(InputAction action, IEnumerable<string> keys)
    {
        Action = action;
        _keys = string.Join(", ", keys);
    }

    /// <summary>Action.</summary>
    public InputAction Action { get; }
    /// <summary>Action label.</summary>
    public string Label => System.Text.RegularExpressions.Regex.Replace(Action.ToString(), "(?<=[a-z])([A-Z])", " $1");

    /// <summary>Key names.</summary>
    [ObservableProperty]
    private string _keys;

    /// <summary>Waiting for a key press.</summary>
    [ObservableProperty]
    private bool _isCapturing;
}

/// <summary>A controller button binding row in the settings screen.</summary>
public sealed partial class PadRow : ObservableObject
{
    /// <summary>Creates the row.</summary>
    /// <param name="action">Action.</param>
    /// <param name="button">Bound button name.</param>
    public PadRow(InputAction action, string button)
    {
        Action = action;
        _button = button;
    }

    /// <summary>Action.</summary>
    public InputAction Action { get; }
    /// <summary>Action label.</summary>
    public string Label => LabelOf(Action);

    /// <summary>Button name.</summary>
    [ObservableProperty]
    private string _button;

    /// <summary>Waiting for a button press.</summary>
    [ObservableProperty]
    private bool _isCapturing;

    /// <summary>Readable action name.</summary>
    /// <param name="a">Action.</param>
    public static string LabelOf(InputAction a) => System.Text.RegularExpressions.Regex.Replace(a.ToString(), "(?<=[a-z])([A-Z])", " $1");

    /// <summary>Xbox-style button name.</summary>
    /// <param name="b">Button.</param>
    public static string Name(AVAMMB1.Core.Input.GamepadButton b) => b switch
    {
        AVAMMB1.Core.Input.GamepadButton.Up => "D-pad up",
        AVAMMB1.Core.Input.GamepadButton.Down => "D-pad down",
        AVAMMB1.Core.Input.GamepadButton.Left => "D-pad left",
        AVAMMB1.Core.Input.GamepadButton.Right => "D-pad right",
        AVAMMB1.Core.Input.GamepadButton.LeftShoulder => "LB",
        AVAMMB1.Core.Input.GamepadButton.RightShoulder => "RB",
        AVAMMB1.Core.Input.GamepadButton.LeftTrigger => "LT",
        AVAMMB1.Core.Input.GamepadButton.RightTrigger => "RT",
        AVAMMB1.Core.Input.GamepadButton.Back => "View",
        AVAMMB1.Core.Input.GamepadButton.Start => "Start",
        _ => b.ToString(),
    };
}

/// <summary>Settings: audio, display and key bindings.</summary>
public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly MainViewModel _main;
    private readonly ViewModelBase _returnTo;

    /// <summary>Creates the screen.</summary>
    /// <param name="main">Root view model.</param>
    /// <param name="returnTo">Screen to return to.</param>
    public SettingsViewModel(MainViewModel main, ViewModelBase returnTo)
    {
        _main = main;
        _returnTo = returnTo;
        var s = main.Services.Settings;
        _musicVolume = s.MusicVolume;
        _sfxVolume = s.SfxVolume;
        _ambienceVolume = s.AmbienceVolume;
        _fullscreen = s.Fullscreen;
        _showMinimap = s.ShowMinimap;
        _smoothMovement = s.SmoothMovement;
        _fitToWindow = s.FitToWindow;
        _gamepadEnabled = s.GamepadEnabled;
        GamepadStatus = App.Gamepad?.Status ?? "Gamepad support not running";
        _animateMonsters = s.AnimateMonsters;
        _themeIndex = Math.Max(0, Array.IndexOf(ThemeKeys, s.ColorTheme));
        _textScaleIndex = Math.Max(0, Array.IndexOf(TextScales, s.TextScale) is var i and >= 0 ? i : 1);
        _resolutionIndex = Math.Max(0, Array.IndexOf(Resolutions, s.ViewResolution));
        _smoothView = s.SmoothView;
        _pad = new Dictionary<InputAction, AVAMMB1.Core.Input.GamepadButton>(s.GamepadBindings);
        LoadPadRows();
        LoadBindings();
        AudioStatus = main.Services.Audio.Status;
        DataLocation = UserDataPaths.DataDirectory;
    }

    /// <summary>Audio backend status.</summary>
    public string AudioStatus { get; }
    /// <summary>Data directory.</summary>
    public string DataLocation { get; }
    /// <summary>Bindings.</summary>
    public ObservableCollection<BindingRow> Bindings { get; } = new();

    /// <summary>Music volume.</summary>
    [ObservableProperty]
    private int _musicVolume;

    /// <summary>Effects volume.</summary>
    [ObservableProperty]
    private int _sfxVolume;

    /// <summary>Ambient sound volume.</summary>
    [ObservableProperty]
    private int _ambienceVolume;

    /// <summary>Fullscreen.</summary>
    [ObservableProperty]
    private bool _fullscreen;

    /// <summary>Minimap.</summary>
    [ObservableProperty]
    private bool _showMinimap;

    /// <summary>Animated movement.</summary>
    [ObservableProperty]
    private bool _smoothMovement;

    /// <summary>Scale the interface to the window.</summary>
    [ObservableProperty]
    private bool _fitToWindow;

    /// <summary>Use game controllers.</summary>
    [ObservableProperty]
    private bool _gamepadEnabled;

    /// <summary>Controller status line.</summary>
    public string GamepadStatus { get; }

    /// <summary>Animated monsters.</summary>
    [ObservableProperty]
    private bool _animateMonsters;

    private static readonly string[] ThemeKeys = ["Standard", "HighContrast", "ColorblindFriendly"];
    private static readonly int[] TextScales = [90, 100, 115, 130];
    private static readonly int[] Resolutions = [300, 480, 600];
    private readonly Dictionary<InputAction, AVAMMB1.Core.Input.GamepadButton> _pad;
    private PadRow? _capturingPad;

    /// <summary>Theme choices.</summary>
    public string[] ThemeOptions { get; } = ["Standard", "High contrast", "Colour-blind friendly"];
    /// <summary>Text size choices.</summary>
    public string[] TextScaleOptions { get; } = ["90%", "100%", "115%", "130%"];
    /// <summary>3D view resolution choices.</summary>
    public string[] ResolutionOptions { get; } = ["Classic 400 x 300", "Sharp 640 x 480", "High 800 x 600"];

    /// <summary>Selected theme.</summary>
    [ObservableProperty]
    private int _themeIndex;

    /// <summary>Selected text size.</summary>
    [ObservableProperty]
    private int _textScaleIndex;

    /// <summary>Selected 3D view resolution.</summary>
    [ObservableProperty]
    private int _resolutionIndex;

    /// <summary>Smooth scaling of the 3D view.</summary>
    [ObservableProperty]
    private bool _smoothView;

    /// <summary>Controller buttons for exploring.</summary>
    public ObservableCollection<PadRow> PadRows { get; } = new();

    /// <summary>Controller hint text.</summary>
    [ObservableProperty]
    private string _padHint = "Click Rebind, then press a button on the controller. Start always opens the menu.";

    /// <summary>Whether a controller button press is awaited.</summary>
    public bool IsCapturingPad => _capturingPad is not null;

    partial void OnThemeIndexChanged(int value) => PreviewTheme();
    partial void OnTextScaleIndexChanged(int value) => PreviewTheme();

    private void PreviewTheme()
    {
        var s = _main.Services.Settings;
        s.ColorTheme = ThemeKeys[Math.Clamp(ThemeIndex, 0, ThemeKeys.Length - 1)];
        s.TextScale = TextScales[Math.Clamp(TextScaleIndex, 0, TextScales.Length - 1)];
        MainViewModel.ApplyTheme(s);
    }

    private void LoadPadRows()
    {
        PadRows.Clear();
        foreach (var a in Enum.GetValues<InputAction>().Where(a => a is not (InputAction.QuickSave or InputAction.QuickLoad or InputAction.Note)))
        {
            PadRows.Add(new PadRow(a, _pad.TryGetValue(a, out var b) ? PadRow.Name(b) : "-"));
        }
    }

    [RelayCommand]
    private void RebindPad(PadRow row)
    {
        if (_capturingPad is not null)
        {
            _capturingPad.IsCapturing = false;
        }
        _capturingPad = row;
        row.IsCapturing = true;
        PadHint = App.Gamepad is null ? "No controller support is running - rebinding needs a connected controller." : $"Press a controller button for \"{row.Label}\"...";
    }

    /// <summary>Receives a controller button while rebinding.</summary>
    /// <param name="button">Button pressed.</param>
    public void CapturePad(AVAMMB1.Core.Input.GamepadButton button)
    {
        if (_capturingPad is not { } row)
        {
            return;
        }
        row.IsCapturing = false;
        _capturingPad = null;
        if (button == AVAMMB1.Core.Input.GamepadButton.Start)
        {
            PadHint = "Start is reserved for the menu. Choose another button.";
            return;
        }
        var displaced = AVAMMB1.Core.Input.GamepadMapping.Rebind(_pad, row.Action, button);
        LoadPadRows();
        PadHint = $"\"{row.Label}\" is now on {PadRow.Name(button)}." + (displaced is { } d ? $" {PadRow.LabelOf(d)} no longer has a button." : "");
    }

    [RelayCommand]
    private void ClearPad(PadRow row)
    {
        _pad.Remove(row.Action);
        LoadPadRows();
        PadHint = $"\"{row.Label}\" has no controller button now.";
    }

    [RelayCommand]
    private void ResetPad()
    {
        _pad.Clear();
        foreach (var (a, b) in AVAMMB1.Core.Input.GamepadMapping.DefaultExploring())
        {
            _pad[a] = b;
        }
        LoadPadRows();
        PadHint = "Controller buttons reset to defaults.";
    }

    /// <summary>Hint text.</summary>
    [ObservableProperty]
    private string _hint = "Click Rebind, then press a key. Esc cancels.";

    private BindingRow? _capturing;

    partial void OnMusicVolumeChanged(int value) => _main.Services.Audio.SetVolumes(value, SfxVolume, AmbienceVolume);
    partial void OnAmbienceVolumeChanged(int value) => _main.Services.Audio.SetVolumes(MusicVolume, SfxVolume, value);
    partial void OnSfxVolumeChanged(int value)
    {
        _main.Services.Audio.SetVolumes(MusicVolume, value, AmbienceVolume);
        _main.Services.Audio.PlaySfx("coins");
    }

    private void LoadBindings()
    {
        Bindings.Clear();
        foreach (var a in Enum.GetValues<InputAction>())
        {
            Bindings.Add(new BindingRow(a, _main.Services.Settings.KeyBindings.GetValueOrDefault(a) ?? []));
        }
    }

    [RelayCommand]
    private void Rebind(BindingRow row)
    {
        if (_capturing is not null)
        {
            _capturing.IsCapturing = false;
        }
        _capturing = row;
        row.IsCapturing = true;
        Hint = $"Press a key for \"{row.Label}\"...";
    }

    [RelayCommand]
    private void ResetBindings()
    {
        _main.Services.Settings.KeyBindings = GameSettings.DefaultBindings();
        LoadBindings();
        Hint = "Key bindings reset to defaults.";
    }

    [RelayCommand]
    private void Back()
    {
        var s = _main.Services.Settings;
        s.MusicVolume = MusicVolume;
        s.SfxVolume = SfxVolume;
        s.AmbienceVolume = AmbienceVolume;
        var fullscreenChanged = s.Fullscreen != Fullscreen;
        s.Fullscreen = Fullscreen;
        s.ShowMinimap = ShowMinimap;
        s.SmoothMovement = SmoothMovement;
        var layoutChanged = s.FitToWindow != FitToWindow;
        s.FitToWindow = FitToWindow;
        s.GamepadEnabled = GamepadEnabled;
        s.AnimateMonsters = AnimateMonsters;
        s.ViewResolution = Resolutions[Math.Clamp(ResolutionIndex, 0, Resolutions.Length - 1)];
        s.SmoothView = SmoothView;
        s.GamepadBindings = new Dictionary<InputAction, AVAMMB1.Core.Input.GamepadButton>(_pad);
        PreviewTheme();
        _main.Services.SaveSettings();
        if (fullscreenChanged)
        {
            _main.ApplyFullscreen();
        }
        if (layoutChanged)
        {
            _main.ApplyLayout();
        }
        if (_returnTo is GameViewModel)
        {
            _main.ReturnToGame();
        }
        else
        {
            _main.ShowTitle();
        }
    }

    /// <inheritdoc />
    public override bool HandleKey(Key key)
    {
        if (_capturing is { } row)
        {
            row.IsCapturing = false;
            _capturing = null;
            if (key == Key.Escape)
            {
                Hint = "Rebinding cancelled.";
                return true;
            }
            var name = key.ToString();
            // A key can only drive one action.
            foreach (var list in _main.Services.Settings.KeyBindings.Values)
            {
                list.RemoveAll(k => string.Equals(k, name, StringComparison.OrdinalIgnoreCase));
            }
            _main.Services.Settings.KeyBindings[row.Action] = [name];
            _main.Services.Settings.Normalize();
            LoadBindings();
            Hint = $"\"{row.Label}\" is now bound to {name}.";
            return true;
        }
        if (key == Key.Escape)
        {
            Back();
            return true;
        }
        return false;
    }
}

/// <summary>Credits and license screen.</summary>
public sealed partial class CreditsViewModel : ViewModelBase
{
    private readonly MainViewModel _main;

    /// <summary>Creates the screen.</summary>
    /// <param name="main">Root view model.</param>
    public CreditsViewModel(MainViewModel main)
    {
        _main = main;
        try
        {
            using var s = AssetLoader.Open(new Uri("avares://AVAMMB1/Assets/Text/CREDITS.md"));
            using var r = new StreamReader(s);
            Text = r.ReadToEnd();
        }
        catch (Exception ex) when (ex is IOException or FileNotFoundException)
        {
            Text = "CREDITS.md is missing from this build.";
        }
    }

    /// <summary>Credits text.</summary>
    public string Text { get; }

    [RelayCommand]
    private void Back() => _main.ShowTitle();

    /// <inheritdoc />
    public override bool HandleKey(Key key)
    {
        if (key is Key.Escape or Key.Enter)
        {
            Back();
            return true;
        }
        return false;
    }
}

/// <summary>Victory or game-over screen.</summary>
public sealed partial class EndingViewModel : ViewModelBase
{
    private readonly MainViewModel _main;

    /// <summary>Creates the screen.</summary>
    /// <param name="main">Root view model.</param>
    /// <param name="victory">Victory or defeat.</param>
    public EndingViewModel(MainViewModel main, bool victory)
    {
        _main = main;
        Victory = victory;
        var state = main.Services.Session.State;
        Title = victory ? "Victory!" : "The party has fallen";
        Text = victory
            ? main.Services.Content.Config.VictoryText
            : "Darkness closes in. Perhaps a saved game holds a brighter fate...";
        Stats = $"Days in the field: {state.Day}   Gold: {state.Gold}   " +
                string.Join("   ", state.Party.Select(c => $"{c.Name} L{c.Level}"));
        main.Services.Audio.PlayMusic(victory ? "title" : "dungeon");
        main.Services.Audio.PlayAmbience(null);
        CanLoad = main.Services.Saves.MostRecentSlot() is not null;
    }

    /// <summary>Whether this is a victory.</summary>
    public bool Victory { get; }
    /// <summary>Title.</summary>
    public string Title { get; }
    /// <summary>Text.</summary>
    public string Text { get; }
    /// <summary>Stats line.</summary>
    public string Stats { get; }
    /// <summary>Whether a save exists.</summary>
    public bool CanLoad { get; }

    [RelayCommand]
    private void ToTitle() => _main.ShowTitle();

    [RelayCommand]
    private void LoadLast()
    {
        if (_main.Services.Saves.MostRecentSlot() is { } slot)
        {
            _main.LoadSlot(slot);
        }
    }
}
