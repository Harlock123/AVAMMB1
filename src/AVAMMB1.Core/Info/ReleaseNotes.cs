using System.Text.RegularExpressions;

namespace AVAMMB1.Core.Info;

/// <summary>One released version's notes.</summary>
/// <param name="Version">Version, e.g. 1.6.0.</param>
/// <param name="Date">Release date text.</param>
/// <param name="Lines">Plain-text lines: headings ("Added") and bullet items.</param>
public sealed record ReleaseEntry(Version Version, string Date, IReadOnlyList<string> Lines);

/// <summary>Reads the "Keep a Changelog" CHANGELOG.md that ships with the game.</summary>
public static partial class ReleaseNotes
{
    [GeneratedRegex(@"^## \[(?<v>\d+\.\d+\.\d+)\](?:\s*-\s*(?<d>.+))?$")]
    private static partial Regex VersionHeading();

    /// <summary>Parses released versions (newest first); the Unreleased section is skipped.</summary>
    /// <param name="markdown">Changelog text.</param>
    public static IReadOnlyList<ReleaseEntry> Parse(string markdown)
    {
        var list = new List<ReleaseEntry>();
        Version? version = null;
        var date = "";
        var lines = new List<string>();
        void Flush()
        {
            if (version is not null)
            {
                list.Add(new ReleaseEntry(version, date, lines.ToList()));
            }
            lines.Clear();
        }
        foreach (var raw in markdown.Replace("\r", "").Split('\n'))
        {
            if (raw.StartsWith("## ", StringComparison.Ordinal))
            {
                Flush();
                var m = VersionHeading().Match(raw.Trim());
                version = m.Success ? Version.Parse(m.Groups["v"].Value) : null;
                date = m.Success ? m.Groups["d"].Value.Trim() : "";
                continue;
            }
            if (version is null)
            {
                continue;
            }
            var line = raw.TrimEnd();
            if (line.StartsWith("### ", StringComparison.Ordinal))
            {
                lines.Add(line[4..].Trim());
            }
            else if (line.TrimStart().StartsWith("- ", StringComparison.Ordinal))
            {
                var indent = line.Length - line.TrimStart().Length;
                lines.Add(new string(' ', indent) + "• " + Clean(line.TrimStart()[2..]));
            }
            else if (line.Trim().Length > 0 && lines.Count > 0 && lines[^1].TrimStart().StartsWith('•'))
            {
                lines[^1] += " " + Clean(line.Trim()); // continuation of a wrapped bullet
            }
        }
        Flush();
        return list;
    }

    /// <summary>Releases newer than <paramref name="lastSeen"/> (all when it is null).</summary>
    /// <param name="all">Parsed releases.</param>
    /// <param name="lastSeen">The last version the player has seen.</param>
    public static IReadOnlyList<ReleaseEntry> NewerThan(IReadOnlyList<ReleaseEntry> all, Version? lastSeen) =>
        all.Where(e => lastSeen is null || e.Version > lastSeen).ToList();

    private static string Clean(string s) => s.Replace("**", "").Replace("`", "").Replace("*", "");
}
