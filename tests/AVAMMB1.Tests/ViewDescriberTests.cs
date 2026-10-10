using AVAMMB1.Core.Rules;
using AVAMMB1.Core.World;

namespace AVAMMB1.Tests;

/// <summary>The surroundings in words.</summary>
public class ViewDescriberTests
{
    [Fact]
    public void Town_DescribesWhereWhichWayAndWhatIsInSight()
    {
        var s = TestContent.StartedSession();
        (s.State.MapId, s.State.X, s.State.Y, s.State.Facing) = ("brindlemoor", 7, 14, Direction.North);
        s.Explore();
        var text = string.Join("\n", ViewDescriber.Describe(s));
        Assert.Contains("Brindlemoor, square 7,14, facing North", text, StringComparison.Ordinal);
        Assert.Contains("Ahead: open for", text, StringComparison.Ordinal);
        Assert.Contains("Town Fountain (4 squares ahead)", text, StringComparison.Ordinal);
        Assert.Contains("Behind:", text, StringComparison.Ordinal);
        Assert.Contains("open for", ViewDescriber.Brief(s), StringComparison.Ordinal);
    }

    [Fact]
    public void Doors_AndDarkness_AreNamed()
    {
        var s = TestContent.StartedSession();
        var map = s.Content.Map("cellars");
        var (x, y) = Enumerable.Range(0, map.Height).SelectMany(yy => Enumerable.Range(0, map.Width).Select(xx => (xx, yy)))
            .First(c => map.GetWall(c.xx, c.yy, Direction.North) == WallKind.Door);
        (s.State.MapId, s.State.X, s.State.Y, s.State.Facing) = ("cellars", x, y, Direction.North);
        s.State.LightSteps = 0;
        var text = string.Join("\n", ViewDescriber.Describe(s));
        Assert.Contains("Ahead: a door", text, StringComparison.Ordinal);
        Assert.Contains("pitch dark", text, StringComparison.Ordinal);
    }
}
