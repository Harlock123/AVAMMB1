namespace AVAMMB1.Core.Dice;

/// <summary>
/// Abstraction over a random number generator so game rules can be tested deterministically.
/// </summary>
public interface IRandomSource
{
    /// <summary>Returns a random integer in the range [<paramref name="minInclusive"/>, <paramref name="maxExclusive"/>).</summary>
    /// <param name="minInclusive">Inclusive lower bound.</param>
    /// <param name="maxExclusive">Exclusive upper bound.</param>
    int Next(int minInclusive, int maxExclusive);
}

/// <summary>Default <see cref="IRandomSource"/> backed by <see cref="System.Random"/>.</summary>
public sealed class DefaultRandomSource : IRandomSource
{
    private readonly Random _random;

    /// <summary>Creates a random source with an optional fixed seed.</summary>
    /// <param name="seed">Seed for reproducible sequences; <c>null</c> for a time-based seed.</param>
    public DefaultRandomSource(int? seed = null)
    {
        _random = seed is { } s ? new Random(s) : new Random();
    }

    /// <inheritdoc />
    public int Next(int minInclusive, int maxExclusive) =>
        maxExclusive <= minInclusive ? minInclusive : _random.Next(minInclusive, maxExclusive);
}

/// <summary>Convenience helpers for common rolls.</summary>
public static class RandomSourceExtensions
{
    /// <summary>Rolls a single die with the given number of sides (1..sides).</summary>
    /// <param name="rng">The random source.</param>
    /// <param name="sides">Number of sides.</param>
    public static int Die(this IRandomSource rng, int sides) => sides <= 1 ? 1 : rng.Next(1, sides + 1);

    /// <summary>Returns true with the given percentage probability (0-100).</summary>
    /// <param name="rng">The random source.</param>
    /// <param name="percent">Chance in percent.</param>
    public static bool Chance(this IRandomSource rng, int percent) => percent > 0 && rng.Next(0, 100) < percent;

    /// <summary>Picks a random element of a non-empty list.</summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="rng">The random source.</param>
    /// <param name="items">Candidates.</param>
    public static T Pick<T>(this IRandomSource rng, IReadOnlyList<T> items) => items[rng.Next(0, items.Count)];
}
