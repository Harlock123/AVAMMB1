using AVAMMB1.Core.Info;
using AVAMMB1.Core.Persistence;

namespace AVAMMB1.Tests;

public class UpdateCheckTests
{
    [Theory]
    [InlineData("v1.13.0", "1.13.0")]
    [InlineData("1.12.1", "1.12.1")]
    [InlineData(" V2.0.0 ", "2.0.0")]
    [InlineData("v1.13", null)]
    [InlineData("nightly", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Tags_AreReadAsVersions(string? tag, string? expected) =>
        Assert.Equal(expected, UpdateCheck.ParseTag(tag)?.ToString());

    [Fact]
    public void GitHubsAnswer_GivesTheNewestRelease_ButNotDraftsOrPreReleases()
    {
        Assert.Equal(new Version(1, 13, 0), UpdateCheck.LatestFromJson("""{"tag_name":"v1.13.0","draft":false,"prerelease":false,"name":"x"}"""));
        Assert.Null(UpdateCheck.LatestFromJson("""{"tag_name":"v1.13.0","prerelease":true}"""));
        Assert.Null(UpdateCheck.LatestFromJson("""{"tag_name":"v1.13.0","draft":true}"""));
        Assert.Null(UpdateCheck.LatestFromJson("""{"message":"API rate limit exceeded"}"""));
        Assert.Null(UpdateCheck.LatestFromJson("<html>"));
        Assert.Null(UpdateCheck.LatestFromJson("[]"));
    }

    [Fact]
    public void OnlyANewerRelease_IsOffered()
    {
        var running = new Version(1, 12, 0);
        Assert.Equal(new Version(1, 13, 0), UpdateCheck.Offer(running, "1.13.0"));
        Assert.Equal(new Version(1, 12, 1), UpdateCheck.Offer(running, "1.12.1"));
        Assert.Null(UpdateCheck.Offer(running, "1.12.0"));
        Assert.Null(UpdateCheck.Offer(running, "1.11.0"));
        Assert.Null(UpdateCheck.Offer(running, ""));
        Assert.Null(UpdateCheck.Offer(new Version(1, 12, 0, 0), "1.12.0")); // assembly versions have four parts
    }

    [Fact]
    public void GitHubIsAskedAboutOnceADay()
    {
        var now = new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);
        string Ago(double hours) => now.AddHours(-hours).ToString("o", System.Globalization.CultureInfo.InvariantCulture);
        Assert.True(UpdateCheck.IsDue("", now));
        Assert.True(UpdateCheck.IsDue("garbage", now));
        Assert.False(UpdateCheck.IsDue(Ago(1), now));
        Assert.False(UpdateCheck.IsDue(Ago(19), now));
        Assert.True(UpdateCheck.IsDue(Ago(21), now));
        Assert.True(UpdateCheck.IsDue(Ago(-48), now)); // the clock went backwards
    }

    [Fact]
    public void Settings_CheckByDefault_AndRememberTheAnswer()
    {
        var s = new GameSettings();
        Assert.True(s.CheckForUpdates);
        s.LatestRelease = "1.13.0";
        s.LastUpdateCheck = "2026-10-10T12:00:00.0000000Z";
        var back = System.Text.Json.JsonSerializer.Deserialize(
            System.Text.Json.JsonSerializer.Serialize(s, GameJsonContext.Default.GameSettings), GameJsonContext.Default.GameSettings)!;
        Assert.Equal("1.13.0", back.LatestRelease);
        Assert.Equal(s.LastUpdateCheck, back.LastUpdateCheck);
    }
}
