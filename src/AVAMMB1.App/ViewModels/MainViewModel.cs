using Avalonia.Input;
using AVAMMB1.App.Services;
using AVAMMB1.Core.Characters;
using AVAMMB1.Core.Persistence;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AVAMMB1.App.ViewModels;

/// <summary>Root view model: owns the current screen and navigation between screens.</summary>
public sealed partial class MainViewModel : ViewModelBase
{
    /// <summary>Creates the root view model.</summary>
    /// <param name="services">Shared services.</param>
    public MainViewModel(GameServices services)
    {
        Services = services;
        services.Audio.SetVolumes(services.Settings.MusicVolume, services.Settings.SfxVolume, services.Settings.AmbienceVolume);
        ApplyTheme(services.Settings);
        _currentScreen = new TitleViewModel(this);
    }

    /// <summary>Shared services.</summary>
    public GameServices Services { get; }

    /// <summary>Applies the colour theme and text size from the settings.</summary>
    /// <param name="settings">Settings.</param>
    public static void ApplyTheme(AVAMMB1.Core.Persistence.GameSettings settings) =>
        Theming.Theme.Apply(Enum.TryParse<Theming.ThemeKind>(settings.ColorTheme, out var k) ? k : Theming.ThemeKind.Standard, settings.TextScale / 100.0);

    /// <summary>Screen currently displayed.</summary>
    [ObservableProperty]
    private ViewModelBase _currentScreen;

    /// <summary>Raised when the fullscreen preference changes.</summary>
    public event EventHandler? FullscreenChanged;

    /// <summary>Raised when the interface scaling preference changes.</summary>
    public event EventHandler? LayoutChanged;

    /// <summary>Applies the interface scaling setting.</summary>
    public void ApplyLayout() => LayoutChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>Raised when the user asks to quit.</summary>
    public event EventHandler? QuitRequested;

    /// <summary>The live exploration screen, if a game is running.</summary>
    public GameViewModel? Game { get; private set; }

    /// <summary>Shows the title screen.</summary>
    public void ShowTitle()
    {
        Game = null;
        CurrentScreen = new TitleViewModel(this);
    }

    /// <summary>Shows party creation for a new game.</summary>
    public void ShowNewGame() => CurrentScreen = new PartyCreationViewModel(this, recruitMode: false);

    /// <summary>Shows character creation for recruiting at an inn.</summary>
    public void ShowRecruit() => CurrentScreen = new PartyCreationViewModel(this, recruitMode: true);

    /// <summary>Starts a new game with the given characters.</summary>
    /// <param name="party">Party members.</param>
    /// <param name="roster">Extra characters left at the inn.</param>
    public void StartNewGame(IReadOnlyList<Character> party, IReadOnlyList<Character> roster)
    {
        Services.Session.NewGame(party, roster);
        Game = new GameViewModel(this);
        CurrentScreen = Game;
        Game.ShowStory("Welcome", Services.Content.Config.Intro);
    }

    /// <summary>Returns to the running game.</summary>
    public void ReturnToGame()
    {
        if (Game is null)
        {
            ShowTitle();
            return;
        }
        Game.Refresh();
        CurrentScreen = Game;
    }

    /// <summary>Loads a save slot and enters the game.</summary>
    /// <param name="slot">Slot number.</param>
    /// <returns>An error message, or <c>null</c> on success.</returns>
    public string? LoadSlot(int slot)
    {
        try
        {
            var file = Services.Saves.Load(slot);
            Services.Session.Load(file.State);
            Game = new GameViewModel(this);
            CurrentScreen = Game;
            Game.AddMessage($"Loaded \"{file.Name}\".");
            return null;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            return ex.Message;
        }
    }

    /// <summary>Shows the settings screen.</summary>
    /// <param name="returnTo">Screen to return to afterwards.</param>
    public void ShowSettings(ViewModelBase returnTo) => CurrentScreen = new SettingsViewModel(this, returnTo);

    /// <summary>Shows the credits screen.</summary>
    public void ShowCredits() => CurrentScreen = new CreditsViewModel(this);

    /// <summary>Shows the load screen from the title.</summary>
    public void ShowLoad() => CurrentScreen = new SaveLoadViewModel(this, saving: false, onClose: ShowTitle);

    /// <summary>Shows the victory screen.</summary>
    public void ShowVictory() => CurrentScreen = new EndingViewModel(this, victory: true);

    /// <summary>Shows the game over screen.</summary>
    public void ShowGameOver() => CurrentScreen = new EndingViewModel(this, victory: false);

    /// <summary>Applies the fullscreen setting.</summary>
    public void ApplyFullscreen() => FullscreenChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>Requests application exit.</summary>
    public void Quit() => QuitRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Maps a key to a bound action using the current settings.</summary>
    /// <param name="key">Key pressed.</param>
    public InputAction? ActionFor(Key key)
    {
        var name = key.ToString();
        foreach (var (action, keys) in Services.Settings.KeyBindings)
        {
            if (keys.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                return action;
            }
        }
        return null;
    }

    /// <inheritdoc />
    public override bool HandleKey(Key key) => CurrentScreen.HandleKey(key);
}
