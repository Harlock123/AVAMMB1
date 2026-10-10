using AVAMMB1.Core.Characters;
using AVAMMB1.Core.Rules;
using AVAMMB1.Core.Session;

namespace AVAMMB1.Tests;

/// <summary>Hair, beards and renaming.</summary>
public class PortraitStyleTests
{
    private static readonly string[] Knight = ["Doll/legs_plate", "Doll/boots_gray", "Doll/body_plate", "Doll/head_helm_plume", "Doll/shield_knight", "Doll/weapon_long_sword"];

    [Fact]
    public void Layers_DefaultLook_IsTheClassLook()
    {
        Assert.Equal(Knight, PortraitStyles.Layers(Knight, "male", null, null));
        Assert.Contains("Doll/hair_female", PortraitStyles.Layers(["Doll/body_robe_white", "Doll/hair_{sex}"], "female", null, null));
    }

    [Fact]
    public void Layers_HairReplacesTheHelmet_AndSitsWithTheBeardAboveTheArmour()
    {
        var layers = PortraitStyles.Layers(Knight, "male", "long_red", "short_black");
        Assert.DoesNotContain("Doll/head_helm_plume", layers);
        Assert.Equal(["Doll/legs_plate", "Doll/boots_gray", "Doll/body_plate", "Doll/beard_short_black", "Doll/hair_long_red", "Doll/shield_knight", "Doll/weapon_long_sword"], layers);

        // A beard alone keeps the helmet on; unknown styles are ignored.
        Assert.Contains("Doll/head_helm_plume", PortraitStyles.Layers(Knight, "male", null, "long_white"));
        Assert.Equal(Knight, PortraitStyles.Layers(Knight, "male", "no_such_hair", "nor_beard"));
    }

    [Fact]
    public void EveryStyle_HasItsPicture()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "Assets", "Graphics", "Doll");
        if (!Directory.Exists(root))
        {
            root = Path.Combine(FindRepo(), "Assets", "Graphics", "Doll");
        }
        Assert.All(PortraitStyles.Hair, h => Assert.True(File.Exists(Path.Combine(root, $"hair_{h}.png")), h));
        Assert.All(PortraitStyles.Beards, b => Assert.True(File.Exists(Path.Combine(root, $"beard_{b}.png")), b));
    }

    private static string FindRepo()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "AVAMMB1.sln")))
        {
            d = d.Parent;
        }
        return d?.FullName ?? throw new DirectoryNotFoundException("repository root");
    }

    [Fact]
    public void Cycle_GoesThroughNoneAndWraps()
    {
        string[] list = ["a", "b"];
        Assert.Equal("a", PortraitStyles.Cycle(list, null, 1));
        Assert.Equal("b", PortraitStyles.Cycle(list, "a", 1));
        Assert.Null(PortraitStyles.Cycle(list, "b", 1));
        Assert.Equal("b", PortraitStyles.Cycle(list, null, -1));
        Assert.Equal("flowing fair", PortraitStyles.Describe("fem_yellow"));
        Assert.Equal("long red", PortraitStyles.Describe("long_red"));
    }

    [Fact]
    public void Rename_TrimsChecksAndKeepsNamesUnique()
    {
        var s = TestContent.StartedSession();
        var a = s.State.Party[0];
        var b = s.State.Party[1];
        Assert.Contains("known as Sir Reginald Lon from", s.Town.Rename(a, "  Sir Reginald Longname the Bold ")[0].Text, StringComparison.Ordinal);
        Assert.Equal("Sir Reginald Lon", a.Name); // trimmed and cut to 16 letters
        Assert.Equal(MessageKind.Bad, s.Town.Rename(b, "   ")[0].Kind);
        Assert.Equal(MessageKind.Bad, s.Town.Rename(b, a.Name.ToUpperInvariant())[0].Kind);
        Assert.Equal(MessageKind.Good, s.Town.Rename(b, "Wren")[0].Kind);
        Assert.Equal("Wren", b.Name);
    }

    [Fact]
    public void Looks_SurviveASave()
    {
        var s = TestContent.StartedSession();
        s.State.Party[0].Hair = "knot_red";
        s.State.Party[0].Beard = "long_white";
        var json = AVAMMB1.Core.Persistence.SaveGameService.Serialize(new AVAMMB1.Core.Persistence.SaveFile { State = s.State });
        var back = AVAMMB1.Core.Persistence.SaveGameService.Deserialize(json).State.Party[0];
        Assert.Equal(("knot_red", "long_white"), (back.Hair, back.Beard));
    }
}
