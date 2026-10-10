using System.Text.Json;

namespace AVAMMB1.Core.Info;

/// <summary>
/// Decides whether a newer release of the game exists, from GitHub's "latest release" answer. Nothing is
/// downloaded or installed: the title screen only offers a link to the release page.
/// </summary>
public static class UpdateCheck
{
    /// <summary>GitHub's API address for the newest published release.</summary>
    public const string LatestReleaseApi = "https://api.github.com/repos/Harlock123/AVAMMB1/releases/latest";

    /// <summary>The page where players download releases.</summary>
    public const string ReleasesPage = "https://github.com/Harlock123/AVAMMB1/releases/latest";

    /// <summary>How long to wait before asking again.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromHours(20);

    /// <summary>Whether it is time to ask GitHub again.</summary>
    /// <param name="lastCheckUtc">When the last check happened (round-trip "o" format), or "" if never.</param>
    /// <param name="nowUtc">The time now.</param>
    public static bool IsDue(string lastCheckUtc, DateTime nowUtc) =>
        !DateTime.TryParse(lastCheckUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var last)
        || nowUtc - last.ToUniversalTime() >= Interval
        || last.ToUniversalTime() > nowUtc; // a clock set back

    /// <summary>The version a release tag names ("v1.13.0" or "1.13.0"), or null.</summary>
    /// <param name="tag">The tag.</param>
    public static Version? ParseTag(string? tag)
    {
        var text = tag?.Trim().TrimStart('v', 'V');
        return Version.TryParse(text, out var v) && v.Build >= 0 ? new Version(v.Major, v.Minor, v.Build) : null;
    }

    /// <summary>The newest release's version from GitHub's JSON answer, or null for drafts, pre-releases and junk.</summary>
    /// <param name="json">The response body.</param>
    public static Version? LatestFromJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || (root.TryGetProperty("draft", out var draft) && draft.ValueKind == JsonValueKind.True)
                || (root.TryGetProperty("prerelease", out var pre) && pre.ValueKind == JsonValueKind.True)
                || !root.TryGetProperty("tag_name", out var tag) || tag.ValueKind != JsonValueKind.String)
            {
                return null;
            }
            return ParseTag(tag.GetString());
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The version to offer, if <paramref name="latest"/> is newer than the running game.</summary>
    /// <param name="current">The running version.</param>
    /// <param name="latest">The newest known release, as stored in the settings ("" if none).</param>
    public static Version? Offer(Version current, string latest) =>
        ParseTag(latest) is { } v && v > new Version(current.Major, current.Minor, Math.Max(0, current.Build)) ? v : null;
}
