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
        _fullscreen = s.Fullscreen;
        _showMinimap = s.ShowMinimap;
        _smoothMovement = s.SmoothMovement;
        _animateMonsters = s.AnimateMonsters;
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

    /// <summary>Fullscreen.</summary>
    [ObservableProperty]
    private bool _fullscreen;

    /// <summary>Minimap.</summary>
    [ObservableProperty]
    private bool _showMinimap;

    /// <summary>Animated movement.</summary>
    [ObservableProperty]
    private bool _smoothMovement;

    /// <summary>Animated monsters.</summary>
    [ObservableProperty]
    private bool _animateMonsters;

    /// <summary>Hint text.</summary>
    [ObservableProperty]
    private string _hint = "Click Rebind, then press a key. Esc cancels.";

    private BindingRow? _capturing;

    partial void OnMusicVolumeChanged(int value) => _main.Services.Audio.SetVolumes(value, SfxVolume);
    partial void OnSfxVolumeChanged(int value)
    {
        _main.Services.Audio.SetVolumes(MusicVolume, value);
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
        var fullscreenChanged = s.Fullscreen != Fullscreen;
        s.Fullscreen = Fullscreen;
        s.ShowMinimap = ShowMinimap;
        s.SmoothMovement = SmoothMovement;
        s.AnimateMonsters = AnimateMonsters;
        _main.Services.SaveSettings();
        if (fullscreenChanged)
        {
            _main.ApplyFullscreen();
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
