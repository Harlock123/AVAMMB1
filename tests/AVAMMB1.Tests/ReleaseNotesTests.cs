using AVAMMB1.Core.Info;

namespace AVAMMB1.Tests;

public class ReleaseNotesTests
{
    private const string Sample = """
        # Changelog

        ## [Unreleased]
        - not yet

        ## [1.6.0] - 2026-10-09

        ### Added
        - Quest **journal** (`J`), with
          a Clues tab.
          - nested detail

        ## [1.5.0] - 2026-10-08

        ### Fixed
        - Something.
        """;

    [Fact]
    public void Parse_ReadsReleasedVersionsNewestFirst_AndCleansMarkdown()
    {
        var all = ReleaseNotes.Parse(Sample);
        Assert.Equal([new Version(1, 6, 0), new Version(1, 5, 0)], all.Select(e => e.Version));
        var v16 = all[0];
        Assert.Equal("2026-10-09", v16.Date);
        Assert.Equal("Added", v16.Lines[0]);
        Assert.Equal("• Quest journal (J), with a Clues tab.", v16.Lines[1]);
        Assert.Equal("  • nested detail", v16.Lines[2]);
        Assert.Single(ReleaseNotes.NewerThan(all, new Version(1, 5, 0)));
        Assert.Equal(2, ReleaseNotes.NewerThan(all, null).Count);
    }

    [Fact]
    public void ShippedChangelog_HasNotesForTheCurrentVersion()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AVAMMB1.sln")))
        {
            dir = dir.Parent;
        }
        var all = ReleaseNotes.Parse(File.ReadAllText(Path.Combine(dir!.FullName, "CHANGELOG.md")));
        Assert.Equal(new Version(1, 0, 0), all[^1].Version);
        Assert.All(all, e => Assert.NotEmpty(e.Lines));
    }
}
