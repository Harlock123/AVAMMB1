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
        services.Textures.Detailed = services.Settings.DetailedTextures;
        _currentScreen = new TitleViewModel(this);
        ShowWhatsNewIfUpdated();
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

    /// <summary>The running game version (major.minor.build).</summary>
    public static Version GameVersion => typeof(MainViewModel).Assembly.GetName().Version is { } v ? new Version(v.Major, v.Minor, v.Build) : new Version(1, 0, 0);

    /// <summary>All release notes shipped with the game (newest first).</summary>
    public static IReadOnlyList<AVAMMB1.Core.Info.ReleaseEntry> AllReleaseNotes()
    {
        try
        {
            using var s = Avalonia.Platform.AssetLoader.Open(new Uri("avares://AVAMMB1/Assets/Text/CHANGELOG.md"));
            using var r = new StreamReader(s);
            return AVAMMB1.Core.Info.ReleaseNotes.Parse(r.ReadToEnd());
        }
        catch (Exception ex) when (ex is IOException or FileNotFoundException)
        {
            return [];
        }
    }

    /// <summary>After an update, shows the notes for every version the player has not seen yet (once).</summary>
    private void ShowWhatsNewIfUpdated()
    {
        var s = Services.Settings;
        var current = GameVersion;
        if (Services.FreshInstall)
        {
            MarkNotesSeen();
            return;
        }
        if (s.LastSeenVersion == current.ToString())
        {
            return;
        }
        // Settings from before 1.7 never recorded a version: show this release only.
        var all = AllReleaseNotes();
        var entries = Version.TryParse(s.LastSeenVersion, out var lastSeen)
            ? AVAMMB1.Core.Info.ReleaseNotes.NewerThan(all, lastSeen).Where(e => e.Version <= current).ToList()
            : all.Where(e => e.Version == current).ToList();
        MarkNotesSeen();
        if (entries.Count > 0)
        {
            CurrentScreen = new WhatsNewViewModel(entries, ShowTitle);
        }
    }

    private void MarkNotesSeen()
    {
        Services.Settings.LastSeenVersion = GameVersion.ToString();
        Services.SaveSettings();
    }

    /// <summary>Shows the release notes for all versions.</summary>
    public void ShowAllNotes() => CurrentScreen = new WhatsNewViewModel(AllReleaseNotes(), ShowTitle);

    /// <summary>Shows the Mods screen.</summary>
    public void ShowMods() => CurrentScreen = new ModsViewModel(this);

    /// <summary>Shows the help screen as a full screen (from the title).</summary>
    public void ShowHelp() => CurrentScreen = new HelpViewModel(Services.Settings, ShowTitle);

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
        Services.Session.Difficulty = Services.Settings.Difficulty;
        Services.Session.Survival = Services.Settings.Survival;
        Services.Session.Ironman = Services.Settings.Ironman;
        Services.Session.NewGame(party, roster);
        Game = new GameViewModel(this);
        CurrentScreen = Game;
        Game.ShowStory("Welcome", Services.Content.Config.Intro);
        Game.AutoSave(null); // an ironman run has its save from the first step
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
            var missing = file.Mods.Except(Services.Content.Mods.Select(m => m.Id)).ToList();
            try
            {
                Services.Session.Load(file.State);
                Services.RecordAchievements(file.State.Achievements);
            }
            catch (InvalidDataException) when (missing.Count > 0)
            {
                throw new InvalidDataException($"This save needs mod packs that are not active: {string.Join(", ", missing)}. Enable them on the Mods screen and restart.");
            }
            Game = new GameViewModel(this);
            CurrentScreen = Game;
            Game.AddMessage($"Loaded \"{file.Name}\".");
            if (missing.Count > 0)
            {
                Game.AddMessage($"Warning: this save was made with mod packs that are not active ({string.Join(", ", missing)}).");
            }
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

    /// <summary>Today's date, as daily challenges are named.</summary>
    public static string Today => DateTime.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Starts (or, if one is in progress today, continues) the daily challenge.</summary>
    public void StartDailyChallenge()
    {
        var today = Today;
        if (Services.Saves.Exists(SaveGameService.IronmanSlot)
            && Services.Saves.Load(SaveGameService.IronmanSlot).State.DailyChallenge == today
            && LoadSlot(SaveGameService.IronmanSlot) is null)
        {
            return; // today's run continues
        }
        var party = Services.Content.Config.Premades.Select(Services.Session.Factory.CreatePremade).ToList();
        Services.Session.StartDailyChallenge(today, party);
        Game = new GameViewModel(this);
        CurrentScreen = Game;
        Game.ShowStory("Daily Challenge - " + today,
            "Six heroes at the height of their powers stand at the top of today's Depths Below. Every level is the same for everyone who "
            + "goes down today. How deep can you go? Climb back out by any stair up to end the run - or fall, and be remembered.");
        Game.AutoSave(null);
    }

    /// <summary>A daily challenge party has climbed out: record the depth and show how it went.</summary>
    public void EndDailyChallenge()
    {
        var state = Services.Session.State;
        Game?.CountPlayTime();
        var best = Services.HallOfFameStore.Load().DailyBest.GetValueOrDefault(state.DailyChallenge ?? "");
        Services.RecordRun($"Daily challenge {state.DailyChallenge}: climbed out from level {state.DeepestDepth}");
        try
        {
            Services.Saves.Delete(SaveGameService.IronmanSlot);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The run is recorded either way.
        }
        var text = $"The party climbed out of the Depths Below having reached level {state.DeepestDepth}."
            + (state.DeepestDepth > best ? " That is the best today!" : $" Today's best is level {best}.");
        CurrentScreen = new EndingViewModel(this, victory: true, "Challenge complete", text);
    }

    /// <summary>Shows the Hall of Fame.</summary>
    public void ShowHallOfFame() => CurrentScreen = new HallOfFameViewModel(this);

    /// <summary>Shows the credits screen.</summary>
    public void ShowCredits() => CurrentScreen = new CreditsViewModel(this);

    /// <summary>Shows the load screen from the title.</summary>
    public void ShowLoad() => CurrentScreen = new SaveLoadViewModel(this, saving: false, onClose: ShowTitle);

    /// <summary>Shows the victory screen.</summary>
    public void ShowVictory()
    {
        Game?.CountPlayTime();
        Services.RecordRun("Victory");
        if (Services.Session.State.Ironman)
        {
            Game?.AutoSave(null);
        }
        CurrentScreen = new EndingViewModel(this, victory: true);
    }

    /// <summary>Shows the game over screen.</summary>
    public void ShowGameOver()
    {
        var state = Services.Session.State;
        if (state.Ironman)
        {
            Game?.CountPlayTime();
            Services.RecordRun(state.DailyChallenge is { } date
                ? $"Daily challenge {date}: fell on level {state.Depth}"
                : "Fell in " + Services.Session.CurrentMap.Def.Name);
            try
            {
                Services.Saves.Delete(SaveGameService.IronmanSlot); // the run is over
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A save that cannot be deleted will simply load the party as it was.
            }
        }
        CurrentScreen = new EndingViewModel(this, victory: false);
    }

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
