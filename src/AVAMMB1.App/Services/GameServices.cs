using AVAMMB1.App.Rendering;
using AVAMMB1.Core.Content;
using AVAMMB1.Core.Persistence;
using AVAMMB1.Core.Session;

namespace AVAMMB1.App.Services;

/// <summary>Bundle of long-lived services shared by the view models (resolved through DI).</summary>
/// <param name="content">Game content.</param>
/// <param name="session">Running game session.</param>
/// <param name="saves">Save game storage.</param>
/// <param name="settingsStore">Settings storage.</param>
/// <param name="audio">Audio output.</param>
/// <param name="textures">Image cache.</param>
public sealed class GameServices(
    ContentDatabase content,
    GameSession session,
    SaveGameService saves,
    SettingsStore settingsStore,
    IAudioService audio,
    TextureCache textures)
{
    /// <summary>Game content.</summary>
    public ContentDatabase Content { get; } = content;
    /// <summary>Game session.</summary>
    public GameSession Session { get; } = session;
    /// <summary>Saves.</summary>
    public SaveGameService Saves { get; } = saves;
    /// <summary>Settings store.</summary>
    public SettingsStore SettingsStore { get; } = settingsStore;
    /// <summary>Current settings.</summary>
    public GameSettings Settings { get; set; } = settingsStore.Load();
    /// <summary>Audio.</summary>
    public IAudioService Audio { get; } = audio;
    /// <summary>Images.</summary>
    public TextureCache Textures { get; } = textures;

    /// <summary>Builds the portrait for a race/sex/class combination.</summary>
    /// <param name="raceId">Race id.</param>
    /// <param name="sex">Sex.</param>
    /// <param name="classId">Class id (may be null for an unclothed preview).</param>
    public Avalonia.Media.Imaging.Bitmap? Portrait(string raceId, AVAMMB1.Core.Rules.Sex sex, string? classId)
    {
        var sexKey = sex.ToString().ToLowerInvariant();
        var basePath = $"Portraits/{Content.Race(raceId).Portrait}_{sexKey}";
        var layers = classId is not null && Content.Classes.TryGetValue(classId, out var cls)
            ? cls.PortraitLayers.Select(l => l.Replace("{sex}", sexKey, StringComparison.Ordinal)).ToList()
            : new List<string>();
        return Textures.Composite(basePath, layers);
    }

    /// <summary>Persists settings and applies volumes.</summary>
    public void SaveSettings()
    {
        Settings.Normalize();
        Audio.SetVolumes(Settings.MusicVolume, Settings.SfxVolume, Settings.AmbienceVolume);
        try
        {
            SettingsStore.Save(Settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Settings are a convenience; failure to persist them must not crash the game.
        }
    }

    /// <summary>Plays the sound cues attached to messages.</summary>
    /// <param name="messages">Messages.</param>
    public void PlayCues(IEnumerable<GameMessage> messages)
    {
        foreach (var sound in messages.Select(m => m.Sound).Where(s => s is not null).Distinct().Take(3))
        {
            Audio.PlaySfx(sound!);
        }
    }
}
