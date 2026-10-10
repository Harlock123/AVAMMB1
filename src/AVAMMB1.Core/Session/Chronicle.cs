using AVAMMB1.Core.Rules;

namespace AVAMMB1.Core.Session;

/// <summary>An achievement: earned once per game when its condition first holds.</summary>
/// <param name="Id">Id (stored in saves).</param>
/// <param name="Title">Title.</param>
/// <param name="Description">How to earn it.</param>
/// <param name="Earned">Whether the session has earned it.</param>
public sealed record AchievementDef(string Id, string Title, string Description, Func<GameSession, bool> Earned);

/// <summary>The party's statistics and achievements.</summary>
public static class Chronicle
{
    /// <summary>Statistic keys (<see cref="GameState.Stats"/>).</summary>
    public static class Keys
    {
        /// <summary>Battles won.</summary>
        public const string BattlesWon = "battles_won";
        /// <summary>Battles won at night in the open.</summary>
        public const string NightWins = "night_wins";
        /// <summary>Battles run from.</summary>
        public const string BattlesFled = "battles_fled";
        /// <summary>Party members who died in battle.</summary>
        public const string Deaths = "deaths";
        /// <summary>Gold found in battle and in chests.</summary>
        public const string GoldFound = "gold_found";
        /// <summary>Most gold the purse has held.</summary>
        public const string MostGold = "most_gold";
        /// <summary>Chests and hoards opened.</summary>
        public const string Chests = "chests";
        /// <summary>Spells cast.</summary>
        public const string Spells = "spells";
        /// <summary>Secret doors found.</summary>
        public const string Secrets = "secrets";
        /// <summary>Locks picked.</summary>
        public const string Locks = "locks";
        /// <summary>Nights spent at inns.</summary>
        public const string InnNights = "inn_nights";
    }

    private static readonly string[] Towns = ["brindlemoor", "saltreach", "thornwick", "duskmere", "ashkar", "wintermere"];
    private static readonly string[] Bounties = ["dunmore_done", "quell_done", "brega_done", "wren_done", "rashid_done"];

    private static long Stat(GameSession s, string key) => s.State.Stats.GetValueOrDefault(key);
    private static int Slain(GameSession s) => s.State.Kills.Values.Sum();
    private static int BossesSlain(GameSession s) => s.Content.Monsters.Values.Count(m => m.Boss && s.State.Kills.GetValueOrDefault(m.Id) > 0);
    private static bool Flag(GameSession s, string flag) => s.State.Flags.Contains(flag);

    /// <summary>All achievements, in the order the Chronicle lists them.</summary>
    public static readonly IReadOnlyList<AchievementDef> Achievements =
    [
        new("first_blood", "First Blood", "Win a battle.", s => Stat(s, Keys.BattlesWon) >= 1),
        new("exterminator", "Exterminator", "Slay 100 monsters.", s => Slain(s) >= 100),
        new("legend", "Legend of the Realm", "Slay 500 monsters.", s => Slain(s) >= 500),
        new("giant_killer", "Giant Killer", "Defeat a unique monster.", s => BossesSlain(s) >= 1),
        new("bane_of_bosses", "Bane of Tyrants", "Defeat every unique monster in the realm.", s => BossesSlain(s) == s.Content.Monsters.Values.Count(m => m.Boss)),
        new("night_watch", "Night Watch", "Win a battle in the open at night.", s => Stat(s, Keys.NightWins) >= 1),
        new("wayfarer", "Wayfarer", "Visit all six towns.", s => Towns.All(s.State.Explored.ContainsKey)),
        new("cartographer", "Cartographer", "Set foot in twenty different places.", s => s.State.Explored.Count >= 20),
        new("secret_keeper", "Keeper of Secrets", "Find ten secret doors.", s => Stat(s, Keys.Secrets) >= 10),
        new("locksmith", "Locksmith", "Pick ten locks.", s => Stat(s, Keys.Locks) >= 10),
        new("treasure_hunter", "Treasure Hunter", "Open fifty chests and hoards.", s => Stat(s, Keys.Chests) >= 50),
        new("wealthy", "Wealthy", "Hold 10,000 gold between you.", s => Stat(s, Keys.MostGold) >= 10_000),
        new("spellbound", "Spellbound", "Cast 200 spells.", s => Stat(s, Keys.Spells) >= 200),
        new("veteran", "Veteran", "Raise a character to level 15.", s => s.State.Party.Concat(s.State.Roster).Any(c => c.Level >= 15)),
        new("naturalist", "Naturalist", "Fill the bestiary with fifty creatures.", s => s.State.KnownMonsters.Count >= 50),
        new("collector", "Collector", "See sixty different items.", s => s.State.SeenItems.Count >= 60),
        new("bounty_hunter", "Bounty Hunter", "Claim every town's bounty.", s => Bounties.All(f => Flag(s, f))),
        new("concord", "Friend of the Concord", "Silence the Silent Choir.", s => Flag(s, "choir_done")),
        new("thaw", "The Thaw", "End the long winter of Wintermere.", s => Flag(s, "rimefang_heart_returned")),
        new("lodestone", "Keeper of the Lodestone", "Complete the main quest.", s => s.State.Won),
        new("hard_won", "Hard Won", "Complete the main quest on Hard.", s => s.State.Won && s.State.Difficulty == Difficulty.Hard),
        new("survivor", "Survivor", "Reach day 30 in survival mode.", s => s.State.Survival && s.State.Day >= 30),
        new("deep_delver", "Deep Delver", "Reach level 10 of the Depths Below.", s => s.State.DeepestDepth >= 10),
        new("iron_will", "Iron Will", "Complete the main quest in ironman mode.", s => s.State.Won && s.State.Ironman),
    ];

    /// <summary>Earns any achievements whose conditions now hold; returns their announcements.</summary>
    /// <param name="s">Session.</param>
    public static List<GameMessage> Check(GameSession s)
    {
        var state = s.State;
        state.Stats[Keys.MostGold] = Math.Max(state.Stats.GetValueOrDefault(Keys.MostGold), state.Gold + state.Party.Sum(c => (long)c.Gold));
        var log = new List<GameMessage>();
        foreach (var a in Achievements)
        {
            if (!state.Achievements.Contains(a.Id) && a.Earned(s))
            {
                state.Achievements.Add(a.Id);
                log.Add(new($"Achievement: {a.Title}! ({a.Description})", MessageKind.Good, "levelup"));
            }
        }
        return log;
    }
}
