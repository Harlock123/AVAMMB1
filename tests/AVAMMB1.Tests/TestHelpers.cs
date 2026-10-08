using AVAMMB1.Core.Characters;
using AVAMMB1.Core.Content;
using AVAMMB1.Core.Dice;
using AVAMMB1.Core.Rules;
using AVAMMB1.Core.Session;

namespace AVAMMB1.Tests;

/// <summary>Random source returning a scripted sequence (then repeating the last value).</summary>
internal sealed class ScriptedRandom(params int[] values) : IRandomSource
{
    private int _i;

    public int Next(int minInclusive, int maxExclusive)
    {
        var v = values.Length == 0 ? minInclusive : values[Math.Min(_i++, values.Length - 1)];
        return Math.Clamp(v, minInclusive, Math.Max(minInclusive, maxExclusive - 1));
    }
}

internal static class TestContent
{
    private static readonly Lazy<ContentDatabase> Db = new(() => ContentDatabase.Load(new EmbeddedContentSource()));

    public static ContentDatabase Content => Db.Value;

    public static GameSession NewSession(int seed = 42) => new(Content, new DefaultRandomSource(seed));

    public static Dictionary<Stat, int> Stats(int all = 14) => Enum.GetValues<Stat>().ToDictionary(s => s, _ => all);

    public static Character Make(GameSession s, string cls = "knight", string race = "human", int stats = 16, Alignment align = Alignment.Good) =>
        s.Factory.Create("Tester", race, cls, Sex.Male, align, Stats(stats));

    public static GameSession StartedSession(int seed = 42)
    {
        var s = NewSession(seed);
        s.NewGame(Content.Config.Premades.Select(s.Factory.CreatePremade));
        return s;
    }
}
