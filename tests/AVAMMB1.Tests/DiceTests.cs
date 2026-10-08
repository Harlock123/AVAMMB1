using AVAMMB1.Core.Dice;

namespace AVAMMB1.Tests;

public class DiceTests
{
    [Theory]
    [InlineData("3d6", 3, 6, 0)]
    [InlineData("1d4+2", 1, 4, 2)]
    [InlineData("d10-1", 1, 10, -1)]
    [InlineData("2D8", 2, 8, 0)]
    [InlineData("7", 0, 0, 7)]
    public void Parse_ValidExpressions(string text, int count, int sides, int bonus)
    {
        var d = DiceExpression.Parse(text);
        Assert.Equal(new DiceExpression(count, sides, bonus), d);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("2d")]
    [InlineData("40+2d10")]
    public void Parse_InvalidExpressions_Throw(string text) =>
        Assert.Throws<FormatException>(() => DiceExpression.Parse(text));

    [Fact]
    public void Roll_StaysWithinBounds()
    {
        var rng = new DefaultRandomSource(7);
        var d = DiceExpression.Parse("3d6+2");
        for (var i = 0; i < 2000; i++)
        {
            var r = d.Roll(rng);
            Assert.InRange(r, d.Min, d.Max);
        }
        Assert.Equal(5, d.Min);
        Assert.Equal(20, d.Max);
        Assert.Equal(12, d.Average);
    }

    [Fact]
    public void ToString_RoundTrips()
    {
        foreach (var s in new[] { "3d6", "1d4+2", "2d8-1", "5" })
        {
            Assert.Equal(s, DiceExpression.Parse(s).ToString());
        }
    }
}
