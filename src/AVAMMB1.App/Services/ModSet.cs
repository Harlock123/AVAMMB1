using AVAMMB1.Core.Content;
using AVAMMB1.Core.Persistence;

namespace AVAMMB1.App.Services;

/// <summary>The mod packs found on disk, and which of them are switched on.</summary>
/// <param name="All">Every valid pack found.</param>
/// <param name="Active">Packs in use this session (not disabled in the settings).</param>
/// <param name="Problems">Packs that could not be read.</param>
/// <param name="Folders">Folders searched.</param>
public sealed record ModSet(IReadOnlyList<ModPack> All, IReadOnlyList<ModPack> Active, IReadOnlyList<string> Problems, IReadOnlyList<string> Folders)
{
    /// <summary>Finds packs in the user's Mods folder, a Mods folder next to the game and any <c>--mods</c> folder.</summary>
    /// <param name="options">Launch options.</param>
    /// <param name="disabled">Ids switched off in the settings.</param>
    public static ModSet Discover(LaunchOptions options, IEnumerable<string> disabled)
    {
        var folders = new List<string> { UserDataPaths.ModsDirectory, Path.Combine(AppContext.BaseDirectory, "Mods") };
        if (options.ModsDir is { } extra)
        {
            folders.Add(extra);
        }
        var problems = new List<string>();
        var all = ModCatalog.Discover(folders, problems);
        var off = disabled.ToHashSet(StringComparer.Ordinal);
        return new ModSet(all, all.Where(m => !off.Contains(m.Id)).ToList(), problems, folders);
    }

    /// <summary>Folders searched for pack graphics and audio, highest priority first.</summary>
    public IReadOnlyList<string> AssetRoots => Active.Select(m => m.Root).Reverse().ToList();
}
