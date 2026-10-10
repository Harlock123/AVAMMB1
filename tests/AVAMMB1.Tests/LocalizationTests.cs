using System.Text.Json;
using System.Text.RegularExpressions;
using AVAMMB1.Core.Info;
using AVAMMB1.Core.Session;

namespace AVAMMB1.Tests;

/// <summary>
/// Translations: every string the interface shows through <see cref="Loc"/> is listed in
/// Assets/Lang/template.json (run the tests with UPDATE_LANG_TEMPLATE=1 to refresh it), and every
/// language file only translates strings that exist, keeping their placeholders.
/// </summary>
[Collection(LocCollection.Name)]
public partial class LocalizationTests
{
    private static string LangDir => Path.Combine(TestPaths.RepoRoot, "Assets", "Lang");

    [GeneratedRegex(@"\{l:T '([^']*)'\}")]
    private static partial Regex XamlKey();

    [GeneratedRegex(@"Loc\.[TF]\(""((?:[^""\\]|\\.)*)""")]
    private static partial Regex CodeKey();

    [GeneratedRegex(@"\{\d+(?::[^}]*)?\}")]
    private static partial Regex Placeholder();

    /// <summary>Every translatable string: XAML {l:T '...'}, Loc.T/Loc.F in code, and the tips.</summary>
    internal static SortedSet<string> Keys()
    {
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        var src = Path.Combine(TestPaths.RepoRoot, "src");
        foreach (var file in Directory.EnumerateFiles(src, "*.axaml", SearchOption.AllDirectories).Where(f => !f.Contains("/obj/", StringComparison.Ordinal)))
        {
            foreach (Match m in XamlKey().Matches(File.ReadAllText(file)))
            {
                keys.Add(System.Net.WebUtility.HtmlDecode(m.Groups[1].Value));
            }
        }
        foreach (var file in Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories).Where(f => !f.Contains("/obj/", StringComparison.Ordinal)))
        {
            foreach (Match m in CodeKey().Matches(File.ReadAllText(file)))
            {
                keys.Add(Regex.Unescape(m.Groups[1].Value));
            }
        }
        foreach (var tip in Tips.All)
        {
            keys.Add(tip.Title);
            keys.Add(tip.Text);
        }
        return keys;
    }

    [Fact]
    public void TheTemplate_ListsEveryTranslatableString()
    {
        var keys = Keys();
        Assert.True(keys.Count > 250, $"only {keys.Count} strings found");
        var path = Path.Combine(LangDir, "template.json");
        var expected = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["code"] = "xx",
            ["name"] = "Language name (in that language)",
            ["strings"] = keys.ToDictionary(k => k, _ => ""),
        }, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\n";
        if (Environment.GetEnvironmentVariable("UPDATE_LANG_TEMPLATE") == "1")
        {
            File.WriteAllText(path, expected);
        }
        Assert.True(File.Exists(path) && File.ReadAllText(path) == expected,
            "Assets/Lang/template.json is out of date - run the tests with UPDATE_LANG_TEMPLATE=1");
    }

    [Fact]
    public void LanguageFiles_TranslateOnlyRealStrings_AndKeepPlaceholders()
    {
        var keys = Keys();
        var files = Directory.GetFiles(LangDir, "*.json").Where(f => !f.EndsWith("template.json", StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(files);
        foreach (var file in files)
        {
            var pack = LanguagePack.Parse(File.ReadAllText(file));
            Assert.Equal(Path.GetFileNameWithoutExtension(file), pack.Code);
            foreach (var (english, text) in pack.Strings)
            {
                Assert.True(keys.Contains(english), $"{pack.Code}: \"{english}\" is not used anywhere (any more)");
                Assert.Equal(Placeholder().Matches(english).Select(m => m.Value).Order(), Placeholder().Matches(text).Select(m => m.Value).Order());
                foreach (var token in Regex.Matches(english, @"\{[A-Z][A-Za-z]+\}").Select(m => m.Value))
                {
                    Assert.Contains(token, text, StringComparison.Ordinal); // key names in tips
                }
            }
        }
    }

    [Fact]
    public void TheSampleLanguage_TranslatesEverything()
    {
        var keys = Keys();
        var de = LanguagePack.Parse(File.ReadAllText(Path.Combine(LangDir, "de.json")));
        var missing = keys.Where(k => !de.Strings.ContainsKey(k) && k != "AVAM&M").ToList();
        Assert.True(missing.Count == 0, $"{missing.Count} untranslated, e.g. \"{missing.FirstOrDefault()}\"");
    }

    [Fact]
    public void Text_IsLookedUpInTheChosenLanguage_AndFallsBackToEnglish()
    {
        try
        {
            Loc.Use("de");
            Assert.Equal("de", Loc.Current?.Code);
            Assert.Equal("Neues Spiel  (N)", Loc.T("New Game  (N)"));
            Assert.Equal("Something nobody translated", Loc.T("Something nobody translated"));
            Assert.Equal("Version 2.0.0 ist erschienen (du hast 1.0.0)", Loc.F("Version {0} is out (you have {1})", "2.0.0", "1.0.0"));
            Loc.Use("en");
            Assert.Null(Loc.Current);
            Assert.Equal("New Game  (N)", Loc.T("New Game  (N)"));

            Loc.Register(new LanguagePack("tlh", "tlhIngan Hol", new Dictionary<string, string> { ["New Game  (N)"] = "Qapla'" }));
            Loc.Use("tlh");
            Assert.Equal("Qapla'", Loc.T("New Game  (N)"));
            Assert.Contains(Loc.Available, p => p.Code == "tlh");
        }
        finally
        {
            Loc.Use("en");
        }
    }

    [Fact]
    public void BrokenLanguageFiles_AreRefusedWithAMessage()
    {
        Assert.Throws<InvalidDataException>(() => LanguagePack.Parse("{}"));
        Assert.Throws<InvalidDataException>(() => LanguagePack.Parse("not json"));
        Assert.Throws<InvalidDataException>(() => LanguagePack.Parse("""{"code": "", "strings": {}}"""));
    }
}

/// <summary>Tests that switch the interface language run one at a time.</summary>
[CollectionDefinition(Name)]
public class LocCollection
{
    /// <summary>The collection name.</summary>
    public const string Name = "Loc";
}
