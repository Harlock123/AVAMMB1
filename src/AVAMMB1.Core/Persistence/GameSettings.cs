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
    };

    /// <summary>Fills in any actions missing from <see cref="KeyBindings"/> with defaults.</summary>
    public void Normalize()
    {
        MusicVolume = Math.Clamp(MusicVolume, 0, 100);
        SfxVolume = Math.Clamp(SfxVolume, 0, 100);
        AmbienceVolume = Math.Clamp(AmbienceVolume, 0, 100);
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
}
