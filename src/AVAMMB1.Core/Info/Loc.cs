using System.Globalization;
using System.Text.Json;

namespace AVAMMB1.Core.Info;

/// <summary>A translation: the English text of each string, and what it says in this language.</summary>
/// <param name="Code">Language code ("de").</param>
/// <param name="Name">The language's own name ("Deutsch").</param>
/// <param name="Strings">English text to translated text (missing or empty entries stay English).</param>
public sealed record LanguagePack(string Code, string Name, IReadOnlyDictionary<string, string> Strings)
{
    /// <summary>Reads a language file: <c>{"code": "de", "name": "Deutsch", "strings": {"New Game": "Neues Spiel", ...}}</c>.</summary>
    /// <param name="json">File contents.</param>
    /// <exception cref="InvalidDataException">Not a language file.</exception>
    public static LanguagePack Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var code = root.GetProperty("code").GetString() ?? "";
            var name = root.TryGetProperty("name", out var n) ? n.GetString() ?? code : code;
            var strings = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var p in root.GetProperty("strings").EnumerateObject())
            {
                if (p.Value.ValueKind == JsonValueKind.String && p.Value.GetString() is { Length: > 0 } text)
                {
                    strings[p.Name] = text;
                }
            }
            if (code.Length == 0)
            {
                throw new InvalidDataException("A language file needs a \"code\".");
            }
            return new LanguagePack(code, name, strings);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new InvalidDataException("Not a language file: " + ex.Message, ex);
        }
    }
}

/// <summary>
/// Translation of the interface. Strings are written in English in the code and looked up by their English
/// text (<see cref="T"/>), so anything not yet translated simply stays English. Language files live in
/// /Assets/Lang (embedded) and in mod packs.
/// </summary>
public static class Loc
{
    private static readonly List<LanguagePack> Extra = [];
    private static readonly Lazy<IReadOnlyList<LanguagePack>> BuiltIn = new(LoadBuiltIn);

    /// <summary>The language in use, or null for English.</summary>
    public static LanguagePack? Current { get; private set; }

    /// <summary>Every language there is a file for (built in, then from mods).</summary>
    public static IReadOnlyList<LanguagePack> Available => BuiltIn.Value.Concat(Extra).GroupBy(p => p.Code).Select(g => g.Last()).ToList();

    /// <summary>Adds a language from a mod pack (replacing a built-in one with the same code).</summary>
    /// <param name="pack">The language.</param>
    public static void Register(LanguagePack pack) => Extra.Add(pack);

    /// <summary>
    /// Switches language: a code ("de"), "en" for English, or "" for the system's language when there is a
    /// file for it.
    /// </summary>
    /// <param name="code">Language code.</param>
    public static void Use(string? code)
    {
        var wanted = string.IsNullOrEmpty(code) ? CultureInfo.CurrentUICulture.TwoLetterISOLanguageName : code;
        Current = Available.FirstOrDefault(p => string.Equals(p.Code, wanted, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The text in the current language (the English text itself if there is no translation).</summary>
    /// <param name="english">English text.</param>
    public static string T(string english) =>
        Current is { } pack && pack.Strings.TryGetValue(english, out var text) ? text : english;

    /// <summary>A translated format string filled in (<c>{0}</c>-style placeholders, kept by translations).</summary>
    /// <param name="englishFormat">English format string.</param>
    /// <param name="args">Values.</param>
    public static string F(string englishFormat, params object?[] args) =>
        string.Format(CultureInfo.CurrentCulture, T(englishFormat), args);

    private static IReadOnlyList<LanguagePack> LoadBuiltIn()
    {
        var asm = typeof(Loc).Assembly;
        var packs = new List<LanguagePack>();
        foreach (var name in asm.GetManifestResourceNames().Where(n => n.StartsWith("AVAMMB1.Lang/", StringComparison.Ordinal) && !n.EndsWith("template.json", StringComparison.Ordinal)))
        {
            using var stream = asm.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream);
            packs.Add(LanguagePack.Parse(reader.ReadToEnd()));
        }
        return packs.OrderBy(p => p.Name, StringComparer.Ordinal).ToList();
    }
}
