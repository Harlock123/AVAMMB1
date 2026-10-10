namespace AVAMMB1.Core.Rules;

/// <summary>How hard the game is (chosen per game, changeable at any time).</summary>
public enum Difficulty
{
    /// <summary>Weaker monsters, more gold, fewer encounters.</summary>
    Easy,
    /// <summary>The game as balanced.</summary>
    Normal,
    /// <summary>Tougher monsters, less gold, more encounters.</summary>
    Hard,
}

/// <summary>The percentages each <see cref="Difficulty"/> applies.</summary>
public static class DifficultyRules
{
    /// <summary>Monster hit points, in percent of normal.</summary>
    /// <param name="d">Difficulty.</param>
    public static int MonsterHp(Difficulty d) => d switch { Difficulty.Easy => 75, Difficulty.Hard => 130, _ => 100 };

    /// <summary>Damage monsters deal, in percent of normal.</summary>
    /// <param name="d">Difficulty.</param>
    public static int MonsterDamage(Difficulty d) => d switch { Difficulty.Easy => 75, Difficulty.Hard => 125, _ => 100 };

    /// <summary>Gold found on monsters, in percent of normal.</summary>
    /// <param name="d">Difficulty.</param>
    public static int Gold(Difficulty d) => d switch { Difficulty.Easy => 125, Difficulty.Hard => 85, _ => 100 };

    /// <summary>Hit points of unique monsters (bosses), in percent of normal: Hard bosses are tougher still.</summary>
    /// <param name="d">Difficulty.</param>
    public static int BossHp(Difficulty d) => d switch { Difficulty.Easy => 75, Difficulty.Hard => 140, _ => 100 };

    /// <summary>Experience from battles, in percent of normal: on Hard the climb is slower.</summary>
    /// <param name="d">Difficulty.</param>
    public static int Experience(Difficulty d) => d == Difficulty.Hard ? 90 : 100;

    /// <summary>Chance in percent that a random encounter is led by an elite (doubled at night).</summary>
    /// <param name="d">Difficulty.</param>
    public static int EliteChance(Difficulty d) => d == Difficulty.Hard ? 10 : 5;

    /// <summary>Random encounter chance, in percent of normal.</summary>
    /// <param name="d">Difficulty.</param>
    public static int Encounters(Difficulty d) => d switch { Difficulty.Easy => 75, Difficulty.Hard => 125, _ => 100 };

    /// <summary>One-line summary for menus.</summary>
    /// <param name="d">Difficulty.</param>
    public static string Describe(Difficulty d) => d switch
    {
        Difficulty.Easy => "Easy - monsters have 25% less HP and hit 25% softer, 25% more gold, fewer encounters.",
        Difficulty.Hard => "Hard - monsters have 30% more HP (bosses 40%) and hit 25% harder, elites are twice as common, 10% less experience, 15% less gold, more encounters.",
        _ => "Normal - the game as balanced.",
    };
}
