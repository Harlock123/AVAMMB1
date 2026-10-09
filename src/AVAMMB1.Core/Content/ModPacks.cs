using System.Text.Json;
using AVAMMB1.Core.Persistence;

namespace AVAMMB1.Core.Content;

/// <summary>The <c>pack.json</c> manifest of a mod pack.</summary>
public sealed class PackManifest
{
    /// <summary>Unique id (also recorded in save games).</summary>
    public string Id { get; set; } = "";
    /// <summary>Display name.</summary>
    public string Name { get; set; } = "";
    /// <summary>Pack version.</summary>
    public string Version { get; set; } = "1.0";
    /// <summary>Author.</summary>
    public string Author { get; set; } = "";
    /// <summary>What the pack adds.</summary>
    public string Description { get; set; } = "";
}

/// <summary>Extra events for an existing map (lets a pack link its new maps into the base world).</summary>
public sealed class MapPatchDef
{
    /// <summary>Map id to patch.</summary>
    public string Map { get; set; } = "";
    /// <summary>Events appended to that map.</summary>
    public List<MapEventDef> AddEvents { get; set; } = new();
}

/// <summary>A mod pack found on disk: a folder with a <c>pack.json</c> and any content, graphics or audio files.</summary>
/// <param name="Manifest">Manifest.</param>
/// <param name="Root">Folder.</param>
public sealed record ModPack(PackManifest Manifest, string Root)
{
    /// <summary>Pack id.</summary>
    public string Id => Manifest.Id;
    /// <summary>Content files of the pack.</summary>
    public IContentSource Source => new DirectoryContentSource(Root);
}

/// <summary>Finds mod packs in folders such as <c>&lt;user data&gt;/Mods</c>.</summary>
public static class ModCatalog
{
    /// <summary>
    /// Every sub-folder with a valid <c>pack.json</c>, ordered by folder name (later packs win when two
    /// change the same thing). Unreadable manifests are reported in <paramref name="problems"/>.
    /// </summary>
    /// <param name="folders">Folders to search (missing ones are skipped).</param>
    /// <param name="problems">Receives one line per broken pack.</param>
    public static IReadOnlyList<ModPack> Discover(IEnumerable<string> folders, List<string> problems)
    {
        var packs = new List<ModPack>();
        foreach (var folder in folders.Where(Directory.Exists))
        {
            foreach (var dir in Directory.GetDirectories(folder).OrderBy(d => Path.GetFileName(d), StringComparer.OrdinalIgnoreCase))
            {
                var manifest = Path.Combine(dir, "pack.json");
                if (!File.Exists(manifest))
                {
                    continue;
                }
                try
                {
                    var m = JsonSerializer.Deserialize(File.ReadAllText(manifest), GameJsonContext.Default.PackManifest);
                    if (m is null || string.IsNullOrWhiteSpace(m.Id))
                    {
                        problems.Add($"{Path.GetFileName(dir)}: pack.json needs an \"id\".");
                        continue;
                    }
                    if (packs.Any(p => p.Id == m.Id))
                    {
                        problems.Add($"{Path.GetFileName(dir)}: another pack already uses the id '{m.Id}'.");
                        continue;
                    }
                    if (m.Name.Length == 0)
                    {
                        m.Name = m.Id;
                    }
                    packs.Add(new ModPack(m, dir));
                }
                catch (Exception ex) when (ex is JsonException or IOException)
                {
                    problems.Add($"{Path.GetFileName(dir)}: pack.json could not be read ({ex.Message}).");
                }
            }
        }
        return packs;
    }
}
