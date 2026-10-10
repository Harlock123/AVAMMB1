using System.Text.Json;
using System.Text.Json.Nodes;

namespace AVAMMB1.Core.World;

/// <summary>Saves a map from the map editor into a mod pack folder (see docs/modding/MODDING.md).</summary>
public static class MapPackWriter
{
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>
    /// Writes <c>Maps/&lt;id&gt;.json</c> (and a <c>pack.json</c> if the folder has none) and, with an entrance,
    /// a teleport on an existing map into the pack's <c>mapPatches.json</c> that leads to <paramref name="arrival"/>.
    /// An entrance written before for the same map is replaced.
    /// </summary>
    /// <param name="draft">The map.</param>
    /// <param name="folder">The pack's folder.</param>
    /// <param name="packId">The pack id (for a new pack.json).</param>
    /// <param name="entrance">The existing map and square with the way in, or null.</param>
    /// <param name="arrival">Where the way in arrives on the new map.</param>
    /// <returns>The map file's path.</returns>
    /// <exception cref="IOException">The files could not be written.</exception>
    public static string Save(MapDraft draft, string folder, string packId, (string Map, int X, int Y)? entrance, (int X, int Y) arrival)
    {
        Directory.CreateDirectory(Path.Combine(folder, "Maps"));
        var manifest = Path.Combine(folder, "pack.json");
        if (!File.Exists(manifest))
        {
            File.WriteAllText(manifest, new JsonObject
            {
                ["id"] = packId,
                ["name"] = "My maps",
                ["version"] = "1.0",
                ["author"] = "",
                ["description"] = "Maps made with the AVAM&M map editor.",
            }.ToJsonString(Indented) + "\n");
        }
        var path = Path.Combine(folder, "Maps", draft.Meta.Id + ".json");
        File.WriteAllText(path, draft.ToJson());
        var patchFile = Path.Combine(folder, "mapPatches.json");
        var patches = File.Exists(patchFile) && JsonNode.Parse(File.ReadAllText(patchFile)) is JsonArray existing ? existing : new JsonArray();
        var tag = "editor-entrance-" + draft.Meta.Id;
        foreach (var events in patches.OfType<JsonObject>().Select(p => p["addEvents"]).OfType<JsonArray>())
        {
            foreach (var old in events.OfType<JsonObject>().Where(e => e["id"]?.GetValue<string>() == tag).ToList())
            {
                events.Remove(old);
            }
        }
        if (entrance is { } way)
        {
            var target = patches.OfType<JsonObject>().FirstOrDefault(p => p["map"]?.GetValue<string>() == way.Map);
            if (target is null)
            {
                target = new JsonObject { ["map"] = way.Map, ["addEvents"] = new JsonArray() };
                patches.Add((JsonNode)target);
            }
            if (target["addEvents"] is not JsonArray list)
            {
                target["addEvents"] = list = new JsonArray();
            }
            list.Add((JsonNode)new JsonObject
            {
                ["id"] = tag,
                ["x"] = way.X,
                ["y"] = way.Y,
                ["type"] = "teleport",
                ["name"] = draft.Meta.Name,
                ["map"] = draft.Meta.Id,
                ["toX"] = arrival.X,
                ["toY"] = arrival.Y,
                ["facing"] = "North",
                ["feature"] = "stairs_down",
            });
        }
        if (patches.Count > 0 || File.Exists(patchFile))
        {
            File.WriteAllText(patchFile, patches.ToJsonString(Indented) + "\n");
        }
        return path;
    }
}
