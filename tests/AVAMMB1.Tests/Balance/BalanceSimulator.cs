using AVAMMB1.Core.Characters;
using AVAMMB1.Core.Combat;
using AVAMMB1.Core.Content;
using AVAMMB1.Core.Dice;
using AVAMMB1.Core.Items;
using AVAMMB1.Core.Persistence;
using AVAMMB1.Core.Rules;
using AVAMMB1.Core.Session;

namespace AVAMMB1.Tests.Balance;

/// <summary>One stop on the simulated player's route: grind <see cref="Map"/>'s random encounters,
/// using <see cref="Town"/> for the inn, temple, training and shops, until the whole party reaches
/// <see cref="TargetLevel"/>.</summary>
internal sealed record Zone(string Map, string Town, int TargetLevel);

/// <summary>What happened in one zone.</summary>
internal sealed class ZoneReport
{
    public required Zone Zone { get; init; }
    public int EntryLevel { get; set; }
    public int ExitLevel { get; set; }
    public int Battles { get; set; }
    public int Wipes { get; set; }
    public int Deaths { get; set; }
    public int TownTrips { get; set; }
    public int Camps { get; set; }
    public long XpPerBattle { get; set; }
    public long GoldEarned { get; set; }
    /// <summary>Gold from the zone's freely available chests (looted once, on the first visit).</summary>
    public long GoldFromChests { get; set; }
    public int PotionsUsed { get; set; }
    public long SpentHealing { get; set; }
    public long SpentTraining { get; set; }
    public long SpentGear { get; set; }
    public long SpentAcademy { get; set; }
    public long GoldAtExit { get; set; }
    /// <summary>Battles fought while someone could train but the party could not afford it.</summary>
    public int BattlesBlockedByGold { get; set; }
    public bool Reached { get; set; }
    /// <summary>The whole party was dead and there was no gold to raise anyone.</summary>
    public bool Bankrupt { get; set; }

    public override string ToString() =>
        $"{Zone.Map,-10} L{EntryLevel,2}->L{ExitLevel,2} (target {Zone.TargetLevel,2}) {(Reached ? "ok   " : Bankrupt ? "BROKE" : "STUCK")} " +
        $"battles {Battles,4}  wipes {Wipes,2}  deaths {Deaths,3}  town {TownTrips,3}  camps {Camps,3}  xp/battle {XpPerBattle,6}  " +
        $"gold +{GoldEarned,7} chests +{GoldFromChests,6} potions {PotionsUsed,3} heal -{SpentHealing,6} train -{SpentTraining,7} gear -{SpentGear,6} academy -{SpentAcademy,6} = {GoldAtExit,7}  gold-blocked {BattlesBlockedByGold,3}";
}

/// <summary>
/// A simple automated player used to measure progression pacing: it fights a zone's random
/// encounters with straightforward tactics (heal the wounded, casters blast from the back, everyone
/// else attacks, shoots or blocks), camps when hurt, goes to town to cure, raise, train and buy the
/// best gear it can afford, and reloads its last town "save" after a wipe.
/// </summary>
internal sealed class BalanceSimulator
{
    private readonly GameSession _s;
    private readonly HashSet<string> _townsSeen = new();
    private GameState _save;
    private readonly HashSet<string> _looted = new();
    private ZoneReport? _current;
    private bool _profiling;

    public BalanceSimulator(int seed)
    {
        _s = TestContent.StartedSession(seed);
        _save = Clone(_s.State);
    }

    public GameSession Session => _s;

    /// <summary>Battle outcomes; a fight that never ended is recorded as "STUCK" with the last action.</summary>
    public Dictionary<string, int> Outcomes { get; } = new();

    private GameState State => _s.State;

    private static GameState Clone(GameState state) =>
        SaveGameService.Deserialize(SaveGameService.Serialize(new SaveFile { State = state })).State;

    private long TotalGold => State.Gold + State.Party.Sum(c => (long)c.Gold);

    private int MinLevel => State.Party.Min(c => c.Level);

    public List<ZoneReport> Run(IEnumerable<Zone> route, int maxBattlesPerZone)
    {
        var reports = new List<ZoneReport>();
        foreach (var zone in route)
        {
            reports.Add(RunZone(zone, maxBattlesPerZone));
        }
        return reports;
    }

    private ZoneReport RunZone(Zone zone, int maxBattles)
    {
        _townsSeen.Add(zone.Town);
        var report = new ZoneReport { Zone = zone, EntryLevel = MinLevel };
        _current = report;
        long xp = 0;
        Town(report);
        PlaceAtEntrance(zone.Map);
        LootChests(report);
        while (MinLevel < zone.TargetLevel && report.Battles < maxBattles)
        {
            if (State.Party.All(c => !c.IsAlive))
            {
                report.Bankrupt = true;
                break;
            }
            if (State.Party.Any(c => _s.Rules.CanLevelUp(c)) && !CanAffordAnyTraining())
            {
                report.BattlesBlockedByGold++;
            }
            var gold = TotalGold;
            var before = State.Party[0].Experience;
            var outcome = Battle(zone.Map);
            report.Battles++;
            if (outcome != CombatOutcome.Victory)
            {
                report.Wipes++;
                _s.Load(Clone(_save));
                PlaceAtEntrance(zone.Map);
                continue;
            }
            report.GoldEarned += Math.Max(0, TotalGold - gold);
            xp += Math.Max(0, State.Party[0].Experience - before);
            report.Deaths += State.Party.Count(c => c.Has(Condition.Dead));
            SelfCure();
            CureWithPotions();
            if (NeedsTown())
            {
                Town(report);
                PlaceAtEntrance(zone.Map);
            }
            else if (NeedsCamp())
            {
                Camp(report, zone.Map);
            }
        }
        Town(report);
        report.Reached = MinLevel >= zone.TargetLevel;
        report.ExitLevel = MinLevel;
        report.XpPerBattle = report.Battles == 0 ? 0 : xp / report.Battles;
        report.GoldAtExit = TotalGold;
        return report;
    }

    /// <summary>
    /// Profiles a zone at a fixed party level: n battles with camping, a free temple visit after
    /// any death or affliction (its price is recorded), no training. Returns per-battle averages.
    /// </summary>
    public (double Xp, double Gold, double TempleCost, double Deaths, double Wipes) Profile(Zone zone, int level, int n)
    {
        foreach (var c in State.Party)
        {
            c.Experience = Rulebook.XpForLevel(_s.Content.Class(c.Class), level);
            while (_s.Rules.LevelUp(c, _s.Random) is not null)
            {
            }
        }
        _profiling = true;
        _townsSeen.Add(zone.Town);
        State.Gold = 1_000_000;
        var report = new ZoneReport { Zone = zone };
        BuyGearUnlimited();
        _save = Clone(State);
        PlaceAtEntrance(zone.Map);
        var temple = TownEvent(zone.Town, MapEventKind.Temple)!;
        long xp = 0, gold = 0, templeCost = 0, deaths = 0, wipes = 0;
        for (var i = 0; i < n; i++)
        {
            var before = State.Party.Max(c => c.Experience);
            var g = TotalGold;
            if (Battle(zone.Map) != CombatOutcome.Victory)
            {
                wipes++;
                _s.Load(Clone(_save));
                PlaceAtEntrance(zone.Map);
                continue;
            }
            xp += State.Party.Max(c => c.Experience) - before;
            gold += TotalGold - g;
            deaths += State.Party.Count(c => c.Has(Condition.Dead));
            SelfCure();
            foreach (var c in State.Party.Where(Afflicted))
            {
                templeCost += _s.Town.TempleCost(c, temple);
                c.Conditions = Condition.None;
                c.Hp = c.MaxHp;
            }
            if (NeedsCamp())
            {
                Camp(report, zone.Map);
                foreach (var c in State.Party.Where(Afflicted))
                {
                    templeCost += _s.Town.TempleCost(c, temple);
                    c.Conditions = Condition.None;
                    c.Hp = c.MaxHp;
                }
            }
        }
        return ((double)xp / n, (double)gold / n, (double)templeCost / n, (double)deaths / n, (double)wipes / n);
    }

    private void BuyGearUnlimited()
    {
        var keep = State.Gold;
        BuyGear(reserveOverride: 0);
        State.Gold = keep;
    }

    /// <summary>
    /// A player exploring the zone opens its chests once: every treasure event with no flag or item
    /// requirement (boss hoards and quest rewards are not counted).
    /// </summary>
    private void LootChests(ZoneReport report)
    {
        var map = _s.CurrentMap;
        if (!_looted.Add(map.Id))
        {
            return;
        }
        var log = new List<GameMessage>();
        foreach (var ev in map.AllEvents.Where(e => e.Type == MapEventKind.Treasure && e.RequiresFlag is null && e.RequiresItem is null))
        {
            var gold = Math.Max(0, ev.Gold.Roll(_s.Random));
            State.Gold += gold;
            State.Gems += ev.Gems;
            report.GoldFromChests += gold;
            foreach (var id in ev.Items)
            {
                _s.GiveItem(new ItemInstance(id, _s.Content.Item(id).Charges), log);
            }
            foreach (var c in State.Party.Where(c => c.IsAlive))
            {
                c.Experience += ev.Xp;
            }
        }
        _save = Clone(State);
    }

    private (Character Owner, int Index)? FindItem(string itemId)
    {
        foreach (var c in State.Party.Where(c => c.CanAct))
        {
            var i = c.Backpack.FindIndex(it => it.ItemId == itemId);
            if (i >= 0)
            {
                return (c, i);
            }
        }
        return null;
    }

    private void PlaceAtEntrance(string mapId)
    {
        var map = _s.Content.Map(mapId);
        var entrance = map.AllEvents.First(e => e.Type == MapEventKind.Teleport);
        State.MapId = mapId;
        State.X = entrance.X;
        State.Y = entrance.Y;
    }

    // ------------------------------------------------------------------ combat

    private CombatOutcome Battle(string mapId)
    {
        var map = _s.Content.Map(mapId);
        var monsters = new List<MonsterInstance>();
        var groups = _s.Random.Chance(30) ? 2 : 1;
        for (var g = 0; g < groups; g++)
        {
            var entry = PickWeighted(map.Def.Encounters);
            monsters.AddRange(CombatEngine.Spawn(_s.Content.Monster(entry.Monster), entry.Count.Roll(_s.Random), _s.Random));
        }
        var ordered = monsters.Take(8).ToList();
        if (_s.Random.Chance(GameSession.EliteChance) && ordered.FirstOrDefault(m => !m.Def.Boss) is { } leader)
        {
            leader.MakeElite();
        }
        _s.StartCombat(ordered, new StepResult());
        var combat = _s.Combat!;
        combat.Advance();
        var guard = 0;
        string? lastAction = null;
        while (combat.Outcome == CombatOutcome.Ongoing && guard++ < 3000)
        {
            var act = AutoTactics.Choose(_s.Rules, _s.Spells, combat, combat.ActiveCharacter!, State.Party, offensiveSpells: true);
            if (act.Kind == CombatActionKind.UseItem && _current is not null)
            {
                _current.PotionsUsed++;
            }
            lastAction = $"{combat.ActiveCharacter!.Name}({combat.ActiveCharacter.Class}) {act}";
            combat.Act(act);
        }
        var outcome = _s.EndCombat().Outcome;
        var key = guard >= 3000 ? "STUCK " + lastAction : outcome.ToString();
        Outcomes[key] = Outcomes.GetValueOrDefault(key) + 1;
        return outcome;
    }

    private EncounterEntryDef PickWeighted(List<EncounterEntryDef> entries)
    {
        var total = entries.Sum(e => Math.Max(1, e.Weight));
        var roll = _s.Random.Next(0, total);
        foreach (var e in entries)
        {
            roll -= Math.Max(1, e.Weight);
            if (roll < 0)
            {
                return e;
            }
        }
        return entries[^1];
    }

    // ------------------------------------------------------------------ resting and town

    private static bool Afflicted(Character c) =>
        c.Has(Condition.Dead) || c.Has(Condition.Stoned) || c.Has(Condition.Paralyzed) || c.Has(Condition.Poisoned) ||
        c.Has(Condition.Diseased) || c.Has(Condition.Blinded) || c.Has(Condition.Silenced);

    /// <summary>Casters cure, free and raise party members with their own spells, as a player would.</summary>
    private void SelfCure()
    {
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var target in State.Party.Where(Afflicted).ToList())
            {
                var idx = State.Party.IndexOf(target);
                foreach (var caster in State.Party.Where(c => c.IsAlive && c.Sp > 0 && !c.Has(Condition.Unconscious)))
                {
                    var spell = _s.Rules.KnownSpells(caster).Where(sp => sp.Explore && _s.Spells.CanCast(caster, sp, inCombat: false) is null &&
                            (target.Has(Condition.Dead) ? sp.Effect == EffectKind.Raise : sp.Effect == EffectKind.Cure && (sp.Conditions & target.Conditions) != 0))
                        .OrderBy(sp => sp.Cost).FirstOrDefault();
                    if (spell is null)
                    {
                        continue;
                    }
                    var before = target.Conditions;
                    _s.Spells.Cast(caster, spell, State, null, idx, null);
                    if (target.Conditions != before)
                    {
                        changed = true;
                        break;
                    }
                }
            }
        }
    }

    /// <summary>Disease alone only slows healing, so a player would rest it off rather than walk back.</summary>
    private static bool Urgent(Character c) => Afflicted(c) && (c.Conditions & ~Condition.Diseased & ~Condition.Unconscious) != Condition.None;

    /// <summary>Cure potions for what the casters could not fix (poison, paralysis and the like).</summary>
    private void CureWithPotions()
    {
        foreach (var target in State.Party.Where(c => Urgent(c) && !c.Has(Condition.Dead) && !c.Has(Condition.Stoned)).ToList())
        {
            if (FindItem("potion_cure") is not { } found)
            {
                return;
            }
            var r = _s.Spells.UseItem(found.Owner, found.Index, State, null, State.Party.IndexOf(target), null);
            if (r.Success && _current is not null)
            {
                _current.PotionsUsed++;
            }
        }
    }

    private bool NeedsTown() =>
        State.Party.Any(Urgent) || State.Party.Any(c => _s.Rules.CanLevelUp(c)) && CanAffordAnyTraining();

    private bool NeedsCamp()
    {
        var hp = State.Party.Sum(c => Math.Max(0, c.Hp));
        var max = State.Party.Sum(c => c.MaxHp);
        var casters = State.Party.Where(c => c.MaxSp > 0).ToList();
        var lowSp = casters.Count > 0 && casters.Sum(c => c.Sp) * 4 < casters.Sum(c => c.MaxSp);
        return hp * 10 < max * 7 || lowSp || State.Party.Any(c => c.Has(Condition.Unconscious) || c.Hp * 2 < c.MaxHp);
    }

    /// <summary>Camping in the dungeon: a chance of being interrupted, then everyone recovers.</summary>
    private void Camp(ZoneReport report, string mapId)
    {
        report.Camps++;
        var map = _s.Content.Map(mapId);
        if (_s.Random.Chance(Math.Min(40, map.Def.EncounterChance * 4)))
        {
            report.Battles++;
            if (Battle(mapId) != CombatOutcome.Victory)
            {
                report.Wipes++;
                _s.Load(Clone(_save));
                PlaceAtEntrance(mapId);
                return;
            }
            if (State.Party.Any(Afflicted) && !_profiling)
            {
                Town(report);
                PlaceAtEntrance(mapId);
                return;
            }
        }
        var log = new List<GameMessage>();
        foreach (var c in State.Party.Where(c => c.IsAlive))
        {
            _s.RestCharacter(c, log, requireFood: false);
        }
    }

    private MapEventDef? TownEvent(string town, MapEventKind kind) =>
        _s.Content.Map(town).AllEvents.FirstOrDefault(e => e.Type == kind);

    private bool CanAffordAnyTraining()
    {
        var training = _townsSeen.Select(t => TownEvent(t, MapEventKind.Training)).OfType<MapEventDef>().OrderBy(e => e.PriceFactor).FirstOrDefault();
        return training is not null && State.Party.Where(c => _s.Rules.CanLevelUp(c)).Any(c => TownServices.TrainingCost(c, training) <= TotalGold);
    }

    private void Town(ZoneReport report)
    {
        report.TownTrips++;
        var town = report.Zone.Town;
        State.MapId = town;
        State.Party.ForEach(c => c.Conditions &= ~Condition.Asleep);
        _s.Town.PoolAll();

        var temple = TownEvent(town, MapEventKind.Temple)!;
        var gold = TotalGold;
        foreach (var c in State.Party.Where(Afflicted).OrderByDescending(c => c.Has(Condition.Dead)).ToList())
        {
            _s.Town.TempleHeal(c, temple);
        }
        report.SpentHealing += Math.Max(0, gold - TotalGold);

        gold = TotalGold;
        var training = TownEvent(town, MapEventKind.Training)!;
        bool trained;
        do
        {
            trained = false;
            foreach (var c in State.Party.Where(c => _s.Rules.CanLevelUp(c)).OrderBy(c => TownServices.TrainingCost(c, training)).ToList())
            {
                var lvl = c.Level;
                _s.Town.Train(c, training);
                _s.Town.PoolAll();
                trained |= c.Level > lvl;
            }
        }
        while (trained);
        report.SpentTraining += Math.Max(0, gold - TotalGold);

        gold = TotalGold;
        var inn = TownEvent(town, MapEventKind.Inn)!;
        (State.X, State.Y) = (inn.X, inn.Y);
        _s.Town.StayAtInn(inn);
        report.SpentHealing += Math.Max(0, gold - TotalGold);

        gold = TotalGold;
        EquipAndSellLoot();
        report.GoldEarned += Math.Max(0, TotalGold - gold);

        gold = TotalGold;
        BuyPotions();
        BuyGear();
        report.SpentGear += Math.Max(0, gold - TotalGold);

        gold = TotalGold;
        Study();
        report.SpentAcademy += Math.Max(0, gold - TotalGold);
        _s.Town.PoolAll();
        _save = Clone(State);
    }

    /// <summary>Equips loot that beats what a character wears, then sells every other piece of equipment.</summary>
    private void EquipAndSellLoot()
    {
        foreach (var c in State.Party)
        {
            for (var i = c.Backpack.Count - 1; i >= 0; i--)
            {
                var d = _s.Content.Item(c.Backpack[i].ItemId);
                if (d.Slot is not { } slot || !Rulebook.CanUse(c, d))
                {
                    continue;
                }
                var current = c.Equipment.TryGetValue(slot, out var cur) ? Score(_s.Content.Item(cur.ItemId)) : 0;
                if (Score(d) > current + 0.4)
                {
                    _s.Inventory.Equip(c, i);
                }
            }
        }
        // Sell spare equipment and surplus consumables; keep quest items and a couple of each useful potion.
        var keep = new Dictionary<string, int> { ["potion_healing"] = 2, ["potion_cure"] = 2, ["potion_vigor"] = 2, ["potion_mana"] = 2 };
        foreach (var c in State.Party)
        {
            for (var i = c.Backpack.Count - 1; i >= 0; i--)
            {
                var item = c.Backpack[i].ItemId;
                if (_s.Content.Item(item).Kind == ItemKind.Quest)
                {
                    continue;
                }
                if (keep.TryGetValue(item, out var left) && left > 0)
                {
                    keep[item] = left - 1;
                    continue;
                }
                _s.Town.Sell(c, i);
            }
        }
        _s.Town.PoolAll();
    }

    /// <summary>With gold to spare (beyond two training sessions), buys academy points in each class's key statistic.</summary>
    private void Study()
    {
        var academy = _townsSeen.Select(t => TownEvent(t, MapEventKind.Academy)).OfType<MapEventDef>().OrderBy(e => e.PriceFactor).FirstOrDefault();
        if (academy is null)
        {
            return;
        }
        var reserve = State.Party.Sum(c => (long)Rulebook.TrainingCost(c)) * 2;
        var studied = true;
        while (studied)
        {
            studied = false;
            foreach (var c in State.Party.OrderBy(c => c.AcademyPoints))
            {
                var stat = c.Class switch
                {
                    "sorcerer" => Stat.Intellect,
                    "cleric" => Stat.Personality,
                    _ => c.AcademyPoints % 2 == 0 ? Stat.Might : Stat.Accuracy,
                };
                if (TownServices.AcademyBlock(c, stat) is null && TotalGold - TownServices.AcademyCost(c, academy) >= reserve)
                {
                    _s.Town.Study(c, stat, academy);
                    _s.Town.PoolAll();
                    studied = true;
                }
            }
        }
    }

    /// <summary>Keeps two healing and two cure potions in stock when gold beyond the training reserve allows.</summary>
    private void BuyPotions()
    {
        var reserve = State.Party.Sum(c => (long)Rulebook.TrainingCost(c));
        var shops = _townsSeen.SelectMany(t => _s.Content.Map(t).AllEvents.Where(e => e.Type == MapEventKind.Shop && e.Shop is not null)
            .Select(e => _s.Content.Shops[e.Shop!])).Distinct().ToList();
        foreach (var id in new[] { "potion_cure", "potion_healing" })
        {
            var shop = shops.Where(sh => sh.Stock.Contains(id)).OrderBy(sh => sh.PriceFactor).FirstOrDefault();
            if (shop is null)
            {
                continue;
            }
            var price = TownServices.BuyPrice(shop, _s.Content.Item(id));
            var have = State.Party.Sum(c => c.Backpack.Count(i => i.ItemId == id));
            for (var n = have; n < 2 && TotalGold - price >= reserve; n++)
            {
                var buyer = State.Party.Where(c => c.Backpack.Count < Character.BackpackSize).OrderBy(c => c.Backpack.Count).FirstOrDefault();
                if (buyer is null)
                {
                    return;
                }
                _s.Town.Buy(shop, id, buyer);
                _s.Town.PoolAll();
            }
        }
    }

    /// <summary>Gear value used to compare items for a slot (AC, average damage, stat bonuses).</summary>
    private static double Score(ItemDef d) =>
        d.ArmorClass * 2.0 + (d.Damage.Count * (d.Damage.Sides + 1) / 2.0 + d.Damage.Bonus) + d.HitBonus + d.StatBonuses.Values.Sum() * 0.5;

    private void BuyGear(long? reserveOverride = null)
    {
        // Keep enough gold for everyone's next training session.
        var reserve = reserveOverride ?? State.Party.Sum(c => (long)Rulebook.TrainingCost(c));
        var shops = _townsSeen.SelectMany(t => _s.Content.Map(t).AllEvents.Where(e => e.Type == MapEventKind.Shop && e.Shop is not null)
            .Select(e => _s.Content.Shops[e.Shop!])).Distinct().ToList();
        var bought = true;
        while (bought)
        {
            bought = false;
            var best = (Gain: 0.0, Shop: (ShopDef?)null, Item: (ItemDef?)null, Who: (Character?)null);
            foreach (var c in State.Party)
            {
                foreach (var shop in shops)
                {
                    foreach (var id in shop.Stock)
                    {
                        var d = _s.Content.Item(id);
                        if (d.Slot is not { } slot || !Rulebook.CanUse(c, d) || d.Kind == ItemKind.Ring && c.Equipment.ContainsKey(EquipSlot.Ring))
                        {
                            continue;
                        }
                        if (slot == EquipSlot.Shield && c.Equipment.TryGetValue(EquipSlot.Weapon, out var w) && _s.Content.Item(w.ItemId).TwoHanded)
                        {
                            continue;
                        }
                        if (d.TwoHanded && c.Equipment.ContainsKey(EquipSlot.Shield))
                        {
                            continue;
                        }
                        var current = c.Equipment.TryGetValue(slot, out var cur) ? Score(_s.Content.Item(cur.ItemId)) : 0;
                        var gain = Score(d) - current;
                        var price = TownServices.BuyPrice(shop, d);
                        if (gain > 0.4 && TotalGold - price >= reserve && gain / Math.Max(1, price) > best.Gain / Math.Max(1, best.Item is null ? 1 : TownServices.BuyPrice(best.Shop!, best.Item)))
                        {
                            best = (gain, shop, d, c);
                        }
                    }
                }
            }
            if (best.Item is null)
            {
                break;
            }
            var who = best.Who!;
            var n = who.Backpack.Count;
            _s.Town.Buy(best.Shop!, best.Item.Id, who);
            _s.Town.PoolAll();
            if (who.Backpack.Count > n && _s.Inventory.Equip(who, who.Backpack.Count - 1).Success)
            {
                bought = true;
                // Sell what was replaced.
                var old = who.Backpack.Count - 1;
                if (old >= 0 && _s.Content.Item(who.Backpack[old].ItemId).Slot is not null)
                {
                    _s.Town.Sell(who, old);
                    _s.Town.PoolAll();
                }
            }
        }
    }
}
