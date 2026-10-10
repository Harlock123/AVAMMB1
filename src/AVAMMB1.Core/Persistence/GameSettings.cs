using System.Text.Json;

namespace AVAMMB1.Core.Persistence;

/// <summary>Bindable player commands.</summary>
public enum InputAction
{
    /// <summary>Step forward.</summary>
    MoveForward,
    /// <summary>Step backward.</summary>
    MoveBack,
    /// <summary>Turn 90 degrees left.</summary>
    TurnLeft,
    /// <summary>Turn 90 degrees right.</summary>
    TurnRight,
    /// <summary>Side-step left.</summary>
    StrafeLeft,
    /// <summary>Side-step right.</summary>
    StrafeRight,
    /// <summary>Use the location (re-enter a shop, read a sign).</summary>
    Interact,
    /// <summary>Open the automap.</summary>
    Automap,
    /// <summary>Search the surrounding walls for secret doors.</summary>
    Search,
    /// <summary>Open the character / inventory screen.</summary>
    Characters,
    /// <summary>Cast a spell.</summary>
    Cast,
    /// <summary>Rest the party.</summary>
    Rest,
    /// <summary>Quick save.</summary>
    QuickSave,
    /// <summary>Quick load.</summary>
    QuickLoad,
    /// <summary>Open the game menu.</summary>
    Menu,
    /// <summary>Open the quest journal.</summary>
    Journal,
    /// <summary>Write a note on the current automap square.</summary>
    Note,
    /// <summary>Open the help screen.</summary>
    Help,
}

/// <summary>User preferences persisted between sessions.</summary>
public sealed class GameSettings
{
    /// <summary>Music volume 0-100.</summary>
    public int MusicVolume { get; set; } = 60;
    /// <summary>Sound effect volume 0-100.</summary>
    public int SfxVolume { get; set; } = 80;
    /// <summary>Ambient sound volume 0-100.</summary>
    public int AmbienceVolume { get; set; } = 50;
    /// <summary>Run full-screen.</summary>
    public bool Fullscreen { get; set; }
    /// <summary>Ids of mod packs the player has switched off.</summary>
    public List<string> DisabledMods { get; set; } = new();
    /// <summary>The game version whose release notes the player has seen ("" before 1.7).</summary>
    public string LastSeenVersion { get; set; } = "";
    /// <summary>Height of the 3D view's internal image: 300 (400x300, classic), 480 (640x480) or 600 (800x600).</summary>
    public int ViewResolution { get; set; } = 300;
    /// <summary>Save automatically on entering a new area and before boss fights (three rotating slots).</summary>
    public bool Autosave { get; set; } = true;
    /// <summary>Mark where open quests lead on the automap and minimap.</summary>
    public bool QuestMarkers { get; set; } = true;
    /// <summary>Difficulty for new games.</summary>
    public Rules.Difficulty Difficulty { get; set; } = Rules.Difficulty.Normal;
    /// <summary>Survival mode (daily rations) for new games.</summary>
    public bool Survival { get; set; }
    /// <summary>Ironman for new games (one save the game keeps itself; a wipe ends the run).</summary>
    public bool Ironman { get; set; }
    /// <summary>Detailed (128 x 128) wall and floor textures where available.</summary>
    public bool DetailedTextures { get; set; }
    /// <summary>Smooth (filtered) scaling of the 3D view instead of sharp pixels.</summary>
    public bool SmoothView { get; set; }
    /// <summary>Colour theme: Standard, HighContrast or ColorblindFriendly.</summary>
    public string ColorTheme { get; set; } = "Standard";
    /// <summary>Interface zoom in percent (100-150) when the interface is not fitted to the window.</summary>
    public int InterfaceZoom { get; set; } = 100;
    /// <summary>Delay between battle log lines: 0 instant, 1 fast, 2 normal, 3 slow.</summary>
    public int BattleTextSpeed { get; set; }
    /// <summary>Text size in percent (90-130).</summary>
    public int TextScale { get; set; } = 100;
    /// <summary>Scale the whole interface to fit the window (off = fixed 100% size).</summary>
    public bool FitToWindow { get; set; } = true;
    /// <summary>Read game controllers (SDL).</summary>
    public bool GamepadEnabled { get; set; } = true;
    /// <summary>Show the minimap overlay.</summary>
    public bool ShowMinimap { get; set; } = true;
    /// <summary>Animate steps and turns in the 3D view (off = classic instant movement).</summary>
    public bool SmoothMovement { get; set; } = true;
    /// <summary>Animate monsters in combat (idle motion, attacks, hits, deaths).</summary>
    public bool AnimateMonsters { get; set; } = true;
    /// <summary>Controller button for each exploring action (unlisted actions have no button).</summary>
    public Dictionary<InputAction, Input.GamepadButton> GamepadBindings { get; set; } = Input.GamepadMapping.DefaultExploring();
    /// <summary>Key names (Avalonia <c>Key</c> enum names) bound to each action.</summary>
    public Dictionary<InputAction, List<string>> KeyBindings { get; set; } = DefaultBindings();

    /// <summary>The default key bindings.</summary>
    public static Dictionary<InputAction, List<string>> DefaultBindings() => new()
    {
        [InputAction.MoveForward] = ["Up", "W"],
        [InputAction.MoveBack] = ["Down", "S"],
        [InputAction.TurnLeft] = ["Left", "A"],
        [InputAction.TurnRight] = ["Right", "D"],
        [InputAction.StrafeLeft] = ["Q"],
        [InputAction.StrafeRight] = ["E"],
        [InputAction.Interact] = ["Space", "Enter"],
        [InputAction.Automap] = ["M"],
        [InputAction.Search] = ["F"],
        [InputAction.Characters] = ["I", "C"],
        [InputAction.Cast] = ["K"],
        [InputAction.Rest] = ["R"],
        [InputAction.QuickSave] = ["F5"],
        [InputAction.QuickLoad] = ["F9"],
        [InputAction.Menu] = ["Escape"],
        [InputAction.Journal] = ["J"],
        [InputAction.Note] = ["N"],
        [InputAction.Help] = ["F1", "H"],
    };

    /// <summary>Fills in any actions missing from <see cref="KeyBindings"/> with defaults.</summary>
    public void Normalize()
    {
        MusicVolume = Math.Clamp(MusicVolume, 0, 100);
        SfxVolume = Math.Clamp(SfxVolume, 0, 100);
        AmbienceVolume = Math.Clamp(AmbienceVolume, 0, 100);
        TextScale = Math.Clamp(TextScale, 90, 130);
        ViewResolution = ViewResolution switch { >= 600 => 600, >= 480 => 480, _ => 300 };
        if (GamepadBindings is null || GamepadBindings.Count == 0)
        {
            GamepadBindings = Input.GamepadMapping.DefaultExploring();
        }
        foreach (var (action, keys) in DefaultBindings())
        {
            if (!KeyBindings.TryGetValue(action, out var existing) || existing.Count == 0)
            {
                KeyBindings[action] = keys;
            }
        }
    }
}

/// <summary>Loads and saves <see cref="GameSettings"/> as JSON.</summary>
/// <param name="path">Settings file path.</param>
public sealed class SettingsStore(string path)
{
    /// <summary>Settings file path.</summary>
    public string Path => path;

    /// <summary>Whether a settings file already existed (false on a fresh install).</summary>
    public bool Exists => File.Exists(path);

    /// <summary>Loads settings, returning defaults when missing or corrupt.</summary>
    public GameSettings Load()
    {
        GameSettings? s = null;
        try
        {
            if (File.Exists(path))
            {
                s = JsonSerializer.Deserialize(File.ReadAllText(path), GameJsonContext.Default.GameSettings);
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            s = null;
        }
        s ??= new GameSettings();
        s.Normalize();
        return s;
    }

    /// <summary>Saves settings.</summary>
    /// <param name="settings">Settings to write.</param>
    public void Save(GameSettings settings)
    {
        var dir = System.IO.Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
        File.WriteAllText(path, JsonSerializer.Serialize(settings, GameJsonContext.Default.GameSettings));
    }
}

/// <summary>Resolves the OS-appropriate user data directory.</summary>
public static class UserDataPaths
{
    /// <summary>Environment variable that overrides the data directory.</summary>
    public const string OverrideVariable = "AVAMMB1_DATA_DIR";

    /// <summary>
    /// Windows: <c>%APPDATA%\AVAMMB1</c>; macOS: <c>~/Library/Application Support/AVAMMB1</c>;
    /// Linux: <c>$XDG_DATA_HOME/AVAMMB1</c> or <c>~/.local/share/AVAMMB1</c>.
    /// </summary>
    public static string DataDirectory
    {
        get
        {
            var overridden = Environment.GetEnvironmentVariable(OverrideVariable);
            if (!string.IsNullOrWhiteSpace(overridden))
            {
                return overridden;
            }
            const string app = "AVAMMB1";
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (OperatingSystem.IsWindows())
            {
                return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), app);
            }
            if (OperatingSystem.IsMacOS())
            {
                return System.IO.Path.Combine(home, "Library", "Application Support", app);
            }
            var xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            return System.IO.Path.Combine(string.IsNullOrWhiteSpace(xdg) ? System.IO.Path.Combine(home, ".local", "share") : xdg, app);
        }
    }

    /// <summary>Directory for save games.</summary>
    public static string SaveDirectory => System.IO.Path.Combine(DataDirectory, "saves");

    /// <summary>Settings file path.</summary>
    public static string SettingsFile => System.IO.Path.Combine(DataDirectory, "settings.json");

    /// <summary>The Hall of Fame (finished runs, achievements across games).</summary>
    public static string HallOfFameFile => System.IO.Path.Combine(DataDirectory, "halloffame.json");
    /// <summary>Folder for mod packs (one sub-folder per pack).</summary>
    public static string ModsDirectory => System.IO.Path.Combine(DataDirectory, "Mods");
}
