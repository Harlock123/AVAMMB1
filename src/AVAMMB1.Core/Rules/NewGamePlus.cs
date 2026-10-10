namespace AVAMMB1.Core.Rules;

/// <summary>
/// New Game+: after the victory, the same party sets out again into a world reset to its beginning, where
/// every monster has grown stronger (more so with each cycle) and Ascendant treasures can be won.
/// </summary>
public static class NewGamePlus
{
    /// <summary>Treasures found only in New Game+.</summary>
    public static readonly IReadOnlyList<string> AscendantItems =
        ["dawnbringer", "sceptre_order", "staff_ages", "stormstring", "dragonking_plate", "starweave_cloak", "archmage_hat", "ring_ascension", "amulet_ages"];

    /// <summary>Percent chance a boss leaves an Ascendant treasure.</summary>
    public const int BossDropChance = 60;

    /// <summary>Percent chance a strong elite (level 15 or more) leaves one.</summary>
    public const int EliteDropChance = 15;

    /// <summary>
    /// Levels every monster gains: the party's level when the cycle began, and three more for each further
    /// cycle (0 in the first game). The last foes of the first game then fight about as hard against a
    /// party that has beaten it as they did the first time.
    /// </summary>
    /// <param name="cycle">The cycle (0 = the first game).</param>
    /// <param name="partyLevel">The heroes' average level when New Game+ began.</param>
    public static int LevelBoost(int cycle, int partyLevel) => cycle <= 0 ? 0 : Math.Max(2, partyLevel) + 3 * (cycle - 1);

    /// <summary>Hit points in percent: a monster lifted by <paramref name="boost"/> levels has as many as one that high would.</summary>
    /// <param name="level">Its own level.</param>
    /// <param name="boost">Levels gained.</param>
    public static int HpPercent(int level, int boost) => 100 * (level + boost + 2) / (level + 2);

    /// <summary>Damage in percent (grows a little slower than hit points).</summary>
    /// <param name="level">Its own level.</param>
    /// <param name="boost">Levels gained.</param>
    public static int DamagePercent(int level, int boost) => 100 * (level + boost + 4) / (level + 4);

    /// <summary>Extra percent chance of an elite leading a group.</summary>
    /// <param name="cycle">The cycle.</param>
    public static int EliteBonus(int cycle) => 5 * Math.Max(0, cycle);
}
