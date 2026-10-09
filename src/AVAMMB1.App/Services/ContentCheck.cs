using AVAMMB1.Core.Content;
using AVAMMB1.Core.Persistence;

namespace AVAMMB1.App.Services;

/// <summary><c>--check-content</c>: loads the game data and every enabled mod pack and reports problems (for modders).</summary>
public static class ContentCheck
{
    /// <summary>Runs the check.</summary>
    /// <param name="options">Launch options (honours <c>--content</c> and <c>--mods</c>).</param>
    /// <returns>0 when everything loads, 1 otherwise.</returns>
    public static int Run(LaunchOptions options)
    {
        var mods = ModSet.Discover(options, new SettingsStore(UserDataPaths.SettingsFile).Load().DisabledMods);
        Console.WriteLine("Mod folders: " + string.Join(", ", mods.Folders));
        foreach (var m in mods.All)
        {
            var on = mods.Active.Contains(m) ? "on " : "off";
            Console.WriteLine($"  [{on}] {m.Manifest.Name} ({m.Id} {m.Manifest.Version}) - {m.Root}");
        }
        foreach (var p in mods.Problems)
        {
            Console.WriteLine("  problem: " + p);
        }
        try
        {
            IContentSource source = options.ContentDir is { } dir ? new DirectoryContentSource(dir) : new EmbeddedContentSource();
            var db = ContentDatabase.Load(source, mods.Active);
            Console.WriteLine($"Content OK: {db.Maps.Count} maps, {db.Monsters.Count} monsters, {db.Items.Count} items, {db.Spells.Count} spells, {db.Quests.Count} quests.");
            return mods.Problems.Count == 0 ? 0 : 1;
        }
        catch (InvalidDataException ex)
        {
            Console.WriteLine("Content problems:");
            Console.WriteLine(ex.Message);
            return 1;
        }
    }
}
