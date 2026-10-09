using Avalonia;
using Avalonia.Media;

namespace AVAMMB1.App.Theming;

/// <summary>Colour themes for accessibility.</summary>
public enum ThemeKind
{
    /// <summary>The original dark-fantasy palette.</summary>
    Standard,
    /// <summary>Black backgrounds, white text and saturated accents.</summary>
    HighContrast,
    /// <summary>Red/green-safe colours (Okabe-Ito palette): good is blue, bad is orange.</summary>
    ColorblindFriendly,
}

/// <summary>
/// Applies the colour theme and text size as application resources. Views reference the colour keys
/// (and the <c>Font&lt;size&gt;</c> keys) with <c>DynamicResource</c>, so changes apply immediately.
/// </summary>
public static class Theme
{
    // key -> (standard, high contrast, colour-blind friendly)
    private static readonly Dictionary<string, (string Std, string Hc, string Cb)> Colors = new()
    {
        ["Gold"] = ("#E8C468", "#FFFF00", "#E8C468"),
        ["PanelBg"] = ("#16141f", "#000000", "#16141f"),
        ["PanelBorder"] = ("#7a6338", "#FFFFFF", "#7a6338"),
        ["Dim"] = ("#9a96a8", "#E8E8E8", "#a8a4b6"),
        ["HpBrush"] = ("#c0392b", "#FF3030", "#D55E00"),
        ["SpBrush"] = ("#2e6fd1", "#40A0FF", "#56B4E9"),
        ["Info"] = ("#9fd0ff", "#00FFFF", "#d8d0ff"),
        ["Good"] = ("#8fe08f", "#00FF00", "#56B4E9"),
        ["Bad"] = ("#ff8a7a", "#FF6060", "#E69F00"),
        ["Danger"] = ("#ff5040", "#FF3030", "#D55E00"),
        ["Parchment"] = ("#e8d9b0", "#FFFFFF", "#e8d9b0"),
        ["Loot"] = ("#ffd75e", "#FFFF00", "#F0E442"),
        ["Plain"] = ("#c8c8d0", "#FFFFFF", "#c8c8d0"),
        ["WindowBg"] = ("#0c0b12", "#000000", "#0c0b12"),
        ["ScreenBg"] = ("#0b0a10", "#000000", "#0b0a10"),
        ["DialogBg"] = ("#ee15131d", "#FF000000", "#ee15131d"),
        ["ButtonBg"] = ("#2a2438", "#000000", "#2a2438"),
        ["ButtonBorder"] = ("#5a4a2a", "#FFFFFF", "#5a4a2a"),
        ["ButtonFg"] = ("#efe6cf", "#FFFFFF", "#efe6cf"),
        ["ButtonHover"] = ("#3e3452", "#404040", "#3e3452"),
        ["CardBg"] = ("#201c2c", "#000000", "#201c2c"),
        ["CardSelBg"] = ("#2e2840", "#202020", "#2e2840"),
        ["CardDangerBg"] = ("#5a1a1a", "#400000", "#5a3a10"),
        ["BadgeBg"] = ("#aa7a2020", "#FF800000", "#aa7a4a00"),
        ["MapBg"] = ("#0b0b12", "#000000", "#0b0b12"),
        ["MapFloor"] = ("#2a2a3a", "#303030", "#2a2a3a"),
        ["MapGrid"] = ("#1c1c28", "#505050", "#1c1c28"),
        ["MapWall"] = ("#d8d0c0", "#FFFFFF", "#d8d0c0"),
        ["MapDoor"] = ("#e09040", "#FFA000", "#E69F00"),
        ["MapLocked"] = ("#ff4040", "#FF3030", "#D55E00"),
        ["MapSecret"] = ("#c070ff", "#FF00FF", "#CC79A7"),
        ["MapParty"] = ("#ffd75e", "#FFFF00", "#F0E442"),
        ["MapServices"] = ("#5ec8ff", "#00FFFF", "#56B4E9"),
        ["MapPassage"] = ("#c070ff", "#FF00FF", "#CC79A7"),
        ["MapTreasure"] = ("#ffd75e", "#FFFF00", "#F0E442"),
        ["MapFountain"] = ("#40a0ff", "#4080FF", "#0072B2"),
        ["MapQuest"] = ("#ff70b0", "#FF80FF", "#009E73"),
        ["MapEncounter"] = ("#ff5040", "#FF3030", "#D55E00"),
        ["MapNote"] = ("#7fe0c0", "#00FF80", "#FFFFFF"),
        ["MapOther"] = ("#c0c0c0", "#FFFFFF", "#c0c0c0"),
        ["MapSolid"] = ("#6b5b4b", "#808080", "#6b5b4b"),
    };

    /// <summary>Font sizes used by the views; each has a <c>Font&lt;size&gt;</c> resource (dots become underscores).</summary>
    public static readonly double[] FontSizes = [10, 11, 11.5, 12, 12.5, 13, 14, 15, 16, 18, 20, 22, 24, 28, 30, 32, 36, 40, 48, 96];

    private static readonly Dictionary<string, IBrush> Current = new();

    /// <summary>The theme in use.</summary>
    public static ThemeKind Kind { get; private set; }

    /// <summary>Text scale in use (1 = 100%).</summary>
    public static double TextScale { get; private set; } = 1;

    /// <summary>Raised after a change (custom-drawn controls repaint).</summary>
    public static event Action? Changed;

    /// <summary>The brush for a colour key in the current theme.</summary>
    /// <param name="key">Key, e.g. <c>Good</c>.</param>
    public static IBrush Brush(string key)
    {
        if (Current.Count == 0)
        {
            Build(ThemeKind.Standard);
        }
        return Current.TryGetValue(key, out var b) ? b : Brushes.Magenta;
    }

    /// <summary>The resource key for a font size.</summary>
    /// <param name="size">Size in device-independent pixels.</param>
    public static string FontKey(double size) => "Font" + size.ToString(System.Globalization.CultureInfo.InvariantCulture).Replace('.', '_');

    /// <summary>Applies a theme and text scale to the application resources.</summary>
    /// <param name="kind">Theme.</param>
    /// <param name="textScale">Text scale (1 = 100%).</param>
    public static void Apply(ThemeKind kind, double textScale)
    {
        Kind = kind;
        TextScale = Math.Clamp(textScale, 0.8, 1.6);
        Build(kind);
        if (Application.Current is { } app)
        {
            foreach (var (key, brush) in Current)
            {
                app.Resources[key] = brush;
            }
            foreach (var size in FontSizes)
            {
                app.Resources[FontKey(size)] = Math.Round(size * TextScale, 1);
            }
            app.Resources["ControlContentThemeFontSize"] = Math.Round(14 * TextScale, 1);
        }
        Changed?.Invoke();
    }

    private static void Build(ThemeKind kind)
    {
        Current.Clear();
        foreach (var (key, (std, hc, cb)) in Colors)
        {
            var hex = kind switch { ThemeKind.HighContrast => hc, ThemeKind.ColorblindFriendly => cb, _ => std };
            Current[key] = new SolidColorBrush(Color.Parse(hex));
        }
    }
}
