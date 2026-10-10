namespace AVAMMB1.Core.Characters;

/// <summary>Hair and beard choices for character portraits (paper-doll layers in <c>Graphics/Doll</c>).</summary>
public static class PortraitStyles
{
    /// <summary>Hairstyles (each shows the head bare: the class's helmet or hood is left off).</summary>
    public static readonly IReadOnlyList<string> Hair =
    [
        "short_black", "short_red", "short_white", "short_yellow", "long_black", "long_red", "long_white", "long_yellow",
        "fem_black", "fem_red", "fem_white", "fem_yellow", "brown_1", "brown_2", "pigtails_brown", "pigtails_yellow",
        "ponytail_yellow", "knot_red",
    ];

    /// <summary>Beards.</summary>
    public static readonly IReadOnlyList<string> Beards =
    [
        "short_black", "short_red", "short_white", "short_yellow", "long_black", "long_red", "long_white", "long_yellow",
    ];

    private static readonly Dictionary<string, string> Labels = new()
    {
        ["fem_black"] = "flowing black", ["fem_red"] = "flowing red", ["fem_white"] = "flowing white", ["fem_yellow"] = "flowing fair",
        ["brown_1"] = "brown curls", ["brown_2"] = "brown locks", ["pigtails_brown"] = "brown pigtails",
        ["pigtails_yellow"] = "fair pigtails", ["ponytail_yellow"] = "fair ponytail", ["knot_red"] = "red topknot",
    };

    /// <summary>A readable name: "long red", "flowing fair", "brown curls".</summary>
    /// <param name="style">Style id.</param>
    public static string Describe(string style) =>
        Labels.TryGetValue(style, out var label) ? label : style.Replace("yellow", "fair", StringComparison.Ordinal).Replace('_', ' ');

    /// <summary>
    /// The paper-doll layers for a portrait: the class's layers (with <c>{sex}</c> filled in), plus the chosen
    /// beard and hair just above the body armour. Choosing hair leaves the class's helmet or hood off.
    /// </summary>
    /// <param name="classLayers">The class's portrait layers.</param>
    /// <param name="sexKey">"male" or "female".</param>
    /// <param name="hair">Hairstyle, or null for the class's own look.</param>
    /// <param name="beard">Beard, or null for none.</param>
    public static List<string> Layers(IEnumerable<string> classLayers, string sexKey, string? hair, string? beard)
    {
        var layers = classLayers.Select(l => l.Replace("{sex}", sexKey, StringComparison.Ordinal)).ToList();
        var extra = new List<string>();
        if (beard is not null && Beards.Contains(beard))
        {
            extra.Add("Doll/beard_" + beard);
        }
        if (hair is not null && Hair.Contains(hair))
        {
            layers.RemoveAll(l => l.StartsWith("Doll/head_", StringComparison.Ordinal) || l.StartsWith("Doll/hair_", StringComparison.Ordinal));
            extra.Add("Doll/hair_" + hair);
        }
        var body = layers.FindLastIndex(l => l.StartsWith("Doll/body_", StringComparison.Ordinal));
        layers.InsertRange(body + 1, extra);
        return layers;
    }

    /// <summary>The next choice in a list, where -1 means "none" (wraps around both ways).</summary>
    /// <param name="list">Choices.</param>
    /// <param name="current">Current choice, or null.</param>
    /// <param name="step">+1 or -1.</param>
    public static string? Cycle(IReadOnlyList<string> list, string? current, int step)
    {
        var i = current is null ? -1 : IndexOf(list, current);
        var n = list.Count + 1; // positions -1..Count-1
        var next = ((i + 1 + step) % n + n) % n - 1;
        return next < 0 ? null : list[next];
    }

    private static int IndexOf(IReadOnlyList<string> list, string value)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i] == value)
            {
                return i;
            }
        }
        return -1;
    }
}
