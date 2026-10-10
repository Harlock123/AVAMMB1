using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Input;
using Avalonia.Platform;
using AVAMMB1.Core.Persistence;
using AVAMMB1.Core.Rules;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AVAMMB1.App.ViewModels;

/// <summary>A save slot row.</summary>
/// <param name="Info">Slot info.</param>
/// <param name="Picture">Thumbnail of the view when saved, if any.</param>
public sealed record SlotRow(SaveSlotInfo Info, Avalonia.Media.Imaging.Bitmap? Picture)
{
    /// <summary>Slot label.</summary>
    public string Label => Info.IsIronman ? "Ironman" : Info.IsAuto ? $"Auto {Info.Slot - SaveGameService.SlotCount + 1}" : Info.Slot == 0 ? "Quick" : $"Slot {Info.Slot}";
    /// <summary>Description.</summary>
    public string Description => Info.Exists
        ? $"{Info.Name} - {Info.Summary}"
        : Info.IsAuto ? "(empty - written automatically)" : "(empty)";
    /// <summary>When saved and how long played.</summary>
    public string Details => !Info.Exists ? ""
        : Info.SavedUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture) + (Info.PlayTime > TimeSpan.Zero ? $"  -  played {PlayTimeText(Info.PlayTime)}" : "");
    /// <summary>Whether there is a thumbnail.</summary>
    public bool HasPicture => Picture is not null;

    /// <summary>"3h 05m" / "12m".</summary>
    /// <param name="t">Play time.</param>
    public static string PlayTimeText(TimeSpan t) =>
        t.TotalHours >= 1 ? $"{(int)t.TotalHours}h {t.Minutes:00}m" : $"{Math.Max(1, t.Minutes)}m";
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
        Feedback = saving && main.Services.Session.IsActive && main.Services.Session.State.Ironman ? "Ironman: the game keeps a single save, written as you go."
            : saving ? "Choose a slot to save into. (Autosaves are kept separately - see Load.)"
            : Slots.Count == 0 ? "There are no saved games yet." : "Choose a saved game to load (newest first).";
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
        var all = _main.Services.Saves.List();
        // Saving: the quick and manual slots in order. Loading: only real saves, newest first.
        var ironman = _main.Services.Session.IsActive && _main.Services.Session.State.Ironman;
        var shown = Saving && ironman
            ? [all.FirstOrDefault(s => s.IsIronman) ?? new SaveSlotInfo(SaveGameService.IronmanSlot, false, "", DateTime.MinValue, "")]
            : Saving ? all.Where(s => !s.IsAuto && !s.IsIronman) : all.Where(s => s.Exists).OrderByDescending(s => s.SavedUtc);
        foreach (var s in shown)
        {
            Avalonia.Media.Imaging.Bitmap? pic = null;
            try
            {
                pic = s.ThumbnailPath is { } path ? new Avalonia.Media.Imaging.Bitmap(path) : null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                // A missing or damaged picture is simply not shown.
            }
            Slots.Add(new SlotRow(s, pic));
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
                _main.Game?.CountPlayTime();
                _main.Services.Saves.Save(row.Info.Slot, name, session.LocationSummary, session.State, _main.Game?.Thumbnail());
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
        _zoomIndex = Math.Max(0, Array.IndexOf(Zooms, s.InterfaceZoom));
        _battleTextIndex = Math.Clamp(s.BattleTextSpeed, 0, 3);
        _smoothView = s.SmoothView;
        _detailedTextures = s.DetailedTextures;
        InGame = returnTo is GameViewModel && main.Services.Session.IsActive;
        var state = main.Services.Session.State;
        _difficultyIndex = (int)(InGame ? state.Difficulty : s.Difficulty);
        _survival = InGame ? state.Survival : s.Survival;
        _autosave = s.Autosave;
        _questMarkers = s.QuestMarkers;
        _weather = s.Weather;
        _describeSteps = s.DescribeSteps;
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

    private static readonly int[] Zooms = [100, 115, 130, 150];

    /// <summary>Interface zoom choices.</summary>
    public string[] ZoomOptions { get; } = ["100%", "115%", "130%", "150%"];

    /// <summary>Selected interface zoom.</summary>
    [ObservableProperty]
    private int _zoomIndex;

    /// <summary>Battle text speed choices.</summary>
    public string[] BattleTextOptions { get; } = ["Instant", "Fast", "Normal", "Slow"];

    /// <summary>Selected battle text speed.</summary>
    [ObservableProperty]
    private int _battleTextIndex;

    /// <summary>Theme choices.</summary>
    public string[] ThemeOptions { get; } = ["Standard", "High contrast", "Colour-blind friendly"];
    /// <summary>Text size choices.</summary>
    public string[] TextScaleOptions { get; } = ["90%", "100%", "115%", "130%"];
    /// <summary>3D view resolution choices.</summary>
    public string[] ResolutionOptions { get; } = ["Classic 400 x 300", "Sharp 640 x 480", "High 800 x 600"];

    /// <summary>Selected theme.</summary>
    [ObservableProperty]
    private int _themeIndex;

    /// <summary>Whether a game is running (difficulty changes apply to it at once).</summary>
    public bool InGame { get; }

    /// <summary>Where the Game settings apply.</summary>
    public string GameNote => InGame
        ? "Changes apply to the game in progress at once, and to new games."
        : "Used for new games; change it any time during a game here.";

    /// <summary>Difficulty choices.</summary>
    public string[] DifficultyOptions { get; } = ["Easy", "Normal", "Hard"];

    /// <summary>Selected difficulty.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DifficultyText))]
    private int _difficultyIndex;

    /// <summary>What the selected difficulty does.</summary>
    public string DifficultyText => DifficultyRules.Describe((Difficulty)Math.Clamp(DifficultyIndex, 0, 2));

    /// <summary>Survival mode (daily rations).</summary>
    [ObservableProperty]
    private bool _survival;

    /// <summary>Describe each step in the log.</summary>
    [ObservableProperty]
    private bool _describeSteps;

    /// <summary>Snow and rain outdoors.</summary>
    [ObservableProperty]
    private bool _weather;

    /// <summary>Mark where quests lead on the maps.</summary>
    [ObservableProperty]
    private bool _questMarkers;

    /// <summary>Autosave on entering new areas and before boss fights.</summary>
    [ObservableProperty]
    private bool _autosave;

    /// <summary>Selected text size.</summary>
    [ObservableProperty]
    private int _textScaleIndex;

    /// <summary>Selected 3D view resolution.</summary>
    [ObservableProperty]
    private int _resolutionIndex;

    /// <summary>Smooth scaling of the 3D view.</summary>
    [ObservableProperty]
    private bool _smoothView;

    /// <summary>Detailed wall and floor textures.</summary>
    [ObservableProperty]
    private bool _detailedTextures;

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
        var zoom = Zooms[Math.Clamp(ZoomIndex, 0, Zooms.Length - 1)];
        var layoutChanged = s.FitToWindow != FitToWindow || s.InterfaceZoom != zoom;
        s.InterfaceZoom = zoom;
        s.BattleTextSpeed = Math.Clamp(BattleTextIndex, 0, 3);
        s.FitToWindow = FitToWindow;
        s.GamepadEnabled = GamepadEnabled;
        s.AnimateMonsters = AnimateMonsters;
        s.ViewResolution = Resolutions[Math.Clamp(ResolutionIndex, 0, Resolutions.Length - 1)];
        s.SmoothView = SmoothView;
        s.DetailedTextures = DetailedTextures;
        _main.Services.Textures.Detailed = DetailedTextures;
        s.Difficulty = (Difficulty)Math.Clamp(DifficultyIndex, 0, 2);
        s.Survival = Survival;
        s.Autosave = Autosave;
        s.QuestMarkers = QuestMarkers;
        s.Weather = Weather;
        s.DescribeSteps = DescribeSteps;
        if (InGame)
        {
            var state = _main.Services.Session.State;
            if (state.Difficulty != s.Difficulty)
            {
                _main.Game?.AddMessage($"Difficulty is now {s.Difficulty}.");
            }
            if (state.Survival != Survival)
            {
                _main.Game?.AddMessage(Survival ? "Survival mode: the party now eats one food each a day." : "Survival mode is off: food is eaten only when resting.");
            }
            state.Difficulty = s.Difficulty;
            state.Survival = Survival;
        }
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
        var ironman = state.Ironman && !victory;
        Text = victory
            ? main.Services.Content.Config.VictoryText
            : ironman ? "Darkness closes in. This was an ironman run: there is no saved game to return to. The party's deeds are written in the Hall of Fame."
            : "Darkness closes in. Perhaps a saved game holds a brighter fate...";
        Stats = $"Days in the field: {state.Day}   Gold: {state.Gold}   " +
                string.Join("   ", state.Party.Select(c => $"{c.Name} L{c.Level}"));
        main.Services.Audio.PlayMusic(victory ? "title" : "dungeon");
        main.Services.Audio.PlayAmbience(null);
        CanLoad = !ironman && main.Services.Saves.MostRecentSlot() is not null;
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

/// <summary>A finished run in the Hall of Fame.</summary>
/// <param name="Heading">"Victory - Hard, ironman".</param>
/// <param name="Party">Party line.</param>
/// <param name="Details">Days, play time, kills, achievements, date.</param>
/// <param name="Won">Whether the main quest was completed.</param>
public sealed record HallEntryRow(string Heading, string Party, string Details, bool Won);

/// <summary>The Hall of Fame: finished runs and achievements earned in any game.</summary>
public sealed partial class HallOfFameViewModel : ViewModelBase
{
    private readonly MainViewModel _main;

    /// <summary>Creates the screen.</summary>
    /// <param name="main">Root view model.</param>
    public HallOfFameViewModel(MainViewModel main)
    {
        _main = main;
        var hof = main.Services.HallOfFameStore.Load();
        Entries = hof.Entries.Select(e =>
        {
            var mode = string.Join(", ", new[] { e.Difficulty.ToString(), e.Survival ? "survival" : null, e.Ironman ? "ironman" : null }.Where(x => x is not null));
            return new HallEntryRow($"{e.Outcome} - {mode}", string.Join("; ", e.Party),
                $"Day {e.Day} - played {SlotRow.PlayTimeText(TimeSpan.FromSeconds(e.PlaySeconds))} - {e.MonstersSlain:N0} monsters slain - {e.Achievements.Count} achievements"
                + (e.DeepestDepth > 0 ? $" - Depths level {e.DeepestDepth}" : "") + $" - {e.FinishedUtc.ToLocalTime().ToString("d", CultureInfo.CurrentCulture)}",
                e.Won);
        }).ToList();
        Achievements = AVAMMB1.Core.Session.Chronicle.Achievements.Select(a => new AchievementRow(a.Title, a.Description, hof.Achievements.Contains(a.Id))).ToList();
        AchievementCount = $"Achievements earned in any game: {Achievements.Count(a => a.Earned)} of {Achievements.Count}";
        Summary = Entries.Count == 0
            ? "No finished runs yet. Complete the main quest - or fall in an ironman run - to be remembered here."
            : $"{Entries.Count(e => e.Won)} victor{(Entries.Count(e => e.Won) == 1 ? "y" : "ies")} in {Entries.Count} finished run{(Entries.Count == 1 ? "" : "s")}.";
    }

    /// <summary>Finished runs, newest first.</summary>
    public IReadOnlyList<HallEntryRow> Entries { get; }
    /// <summary>Every achievement, marked when earned in any game.</summary>
    public IReadOnlyList<AchievementRow> Achievements { get; }
    /// <summary>"Achievements earned in any game: 7 of 23".</summary>
    public string AchievementCount { get; }
    /// <summary>"2 victories in 3 finished runs".</summary>
    public string Summary { get; }

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
