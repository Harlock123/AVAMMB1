using System.Globalization;
using System.Text.RegularExpressions;

namespace AVAMMB1.Core.Dice;

/// <summary>
/// An immutable dice expression of the form <c>NdS+B</c> (for example <c>2d6+1</c>, <c>d8</c>, <c>5</c>).
/// </summary>
/// <param name="Count">Number of dice rolled.</param>
/// <param name="Sides">Sides per die.</param>
/// <param name="Bonus">Flat modifier added to the sum.</param>
public readonly partial record struct DiceExpression(int Count, int Sides, int Bonus)
{
    /// <summary>An expression that always yields zero.</summary>
    public static DiceExpression Zero => new(0, 0, 0);

    /// <summary>Smallest possible result.</summary>
    public int Min => Count * (Sides > 0 ? 1 : 0) + Bonus;

    /// <summary>Largest possible result.</summary>
    public int Max => Count * Sides + Bonus;

    /// <summary>Average result, rounded down.</summary>
    public int Average => (int)Math.Floor(Count * (Sides + 1) / 2.0 + Bonus);

    /// <summary>Rolls the expression.</summary>
    /// <param name="rng">Random source to use.</param>
    public int Roll(IRandomSource rng)
    {
        var total = Bonus;
        for (var i = 0; i < Count; i++)
        {
            total += rng.Die(Sides);
        }
        return total;
    }

    /// <summary>Parses text such as <c>3d6</c>, <c>1d4+2</c>, <c>d10-1</c> or <c>7</c>.</summary>
    /// <param name="text">Expression text.</param>
    /// <exception cref="FormatException">Thrown when the text is not a valid expression.</exception>
    public static DiceExpression Parse(string text)
    {
        if (!TryParse(text, out var result))
        {
            throw new FormatException($"Invalid dice expression '{text}'.");
        }
        return result;
    }

    /// <summary>Attempts to parse a dice expression.</summary>
    /// <param name="text">Expression text.</param>
    /// <param name="result">The parsed expression when successful.</param>
    public static bool TryParse(string? text, out DiceExpression result)
    {
        result = Zero;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }
        var m = DicePattern().Match(text.Replace(" ", string.Empty, StringComparison.Ordinal));
        if (!m.Success)
        {
            return false;
        }
        if (m.Groups["flat"].Success)
        {
            result = new DiceExpression(0, 0, int.Parse(m.Groups["flat"].Value, CultureInfo.InvariantCulture));
            return true;
        }
        var count = m.Groups["count"].Success && m.Groups["count"].Value.Length > 0
            ? int.Parse(m.Groups["count"].Value, CultureInfo.InvariantCulture) : 1;
        var sides = int.Parse(m.Groups["sides"].Value, CultureInfo.InvariantCulture);
        var bonus = m.Groups["bonus"].Success ? int.Parse(m.Groups["bonus"].Value, CultureInfo.InvariantCulture) : 0;
        result = new DiceExpression(count, sides, bonus);
        return true;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        if (Count == 0 || Sides == 0)
        {
            return Bonus.ToString(CultureInfo.InvariantCulture);
        }
        var b = Bonus == 0 ? string.Empty : Bonus > 0 ? $"+{Bonus}" : Bonus.ToString(CultureInfo.InvariantCulture);
        return $"{Count}d{Sides}{b}";
    }

    [GeneratedRegex(@"^(?:(?<flat>-?\d+)|(?<count>\d*)[dD](?<sides>\d+)(?<bonus>[+-]\d+)?)$")]
    private static partial Regex DicePattern();
}
