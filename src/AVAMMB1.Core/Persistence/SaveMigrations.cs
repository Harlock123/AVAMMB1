using System.Text.Json.Nodes;

namespace AVAMMB1.Core.Persistence;

/// <summary>
/// Upgrades save files written by older versions of the game, one format version at a time, on the
/// raw JSON before it is turned into objects. Add a step here whenever <see cref="SaveFile.CurrentVersion"/>
/// is raised, and keep a real save from the old version in the test fixtures.
/// </summary>
public static class SaveMigrations
{
    private static readonly Dictionary<int, Action<JsonObject>> Steps = new()
    {
        [1] = UpgradeFrom1,
    };

    /// <summary>The format version recorded in a save (1 when missing).</summary>
    /// <param name="save">Save file JSON.</param>
    public static int VersionOf(JsonObject save) =>
        save.TryGetPropertyValue("version", out var v) && v is JsonValue value && value.TryGetValue<int>(out var n) ? n : 1;

    /// <summary>Upgrades a save in place to <see cref="SaveFile.CurrentVersion"/>.</summary>
    /// <param name="save">Save file JSON.</param>
    /// <returns>The version the file had before upgrading.</returns>
    public static int Upgrade(JsonObject save)
    {
        var original = VersionOf(save);
        for (var v = original; v < SaveFile.CurrentVersion; v++)
        {
            if (!Steps.TryGetValue(v, out var step))
            {
                throw new InvalidDataException($"No upgrade path from save format {v}.");
            }
            step(save);
            save["version"] = v + 1;
        }
        return original;
    }

    /// <summary>
    /// 1 -> 2 (AVAM&amp;M 1.0-1.4 -> 1.5): drop values that were computed rather than stored, and make sure
    /// fields added during 1.x (personal gold, learned spells, found secrets) are present.
    /// </summary>
    private static void UpgradeFrom1(JsonObject save)
    {
        if (save["state"] is not JsonObject state)
        {
            return;
        }
        state.Remove("day");
        state.Remove("totalGold");
        state.TryAdd("foundSecrets", new JsonArray());
        foreach (var listName in new[] { "party", "roster" })
        {
            if (state[listName] is not JsonArray list)
            {
                continue;
            }
            foreach (var c in list.OfType<JsonObject>())
            {
                foreach (var computed in new[] { "isAlive", "canAct", "statusText", "backpackFull" })
                {
                    c.Remove(computed);
                }
                c.TryAdd("gold", 0);
                c.TryAdd("learnedSpells", new JsonArray());
            }
        }
        save.TryAdd("gameVersion", "1.x");
    }
}
