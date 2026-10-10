using AVAMMB1.Core.Characters;
using AVAMMB1.Core.Combat;
using AVAMMB1.Core.Content;
using AVAMMB1.Core.Dice;
using AVAMMB1.Core.Items;
using AVAMMB1.Core.Magic;
using AVAMMB1.Core.Rules;
using AVAMMB1.Core.World;

namespace AVAMMB1.Core.Session;

/// <summary>Random encounters and battles.</summary>
public sealed partial class GameSession
{
    /// <summary>Percent chance that a random encounter is led by an elite monster.</summary>
    public const int EliteChance = 5; // on Normal; see DifficultyRules.EliteChance

    private void TryRandomEncounter(StepResult result, int chance)
    {
        var map = CurrentMap;
        if (chance < 100)
        {
            chance = chance * DifficultyRules.Encounters(State.Difficulty) / 100;
        }
        if (map.Def.Encounters.Count == 0 || !Random.Chance(chance))
        {
            return;
        }
        var monsters = new List<MonsterInstance>();
        var groups = Random.Chance(30) ? 2 : 1;
        var night = IsNightOutside;
        for (var g = 0; g < groups; g++)
        {
            var table = night && map.Def.NightEncounters.Count > 0 && Random.Chance(50) ? map.Def.NightEncounters : map.Def.Encounters;
            var entry = PickWeighted(table);
            monsters.AddRange(CombatEngine.Spawn(Content.Monster(entry.Monster), entry.Count.Roll(Random), Random));
        }
        var ordered = monsters.Take(8).ToList();
        // Now and then a group is led by an elite: tougher, but worth far more.
        var elite = DifficultyRules.EliteChance(State.Difficulty);
        if (Random.Chance(night ? elite * 2 : elite) && ordered.FirstOrDefault(m => !m.Def.Boss) is { } leader)
        {
            leader.MakeElite();
        }
        StartCombat(ordered, result);
        _combatEventKey = null;
    }

    private EncounterEntryDef PickWeighted(List<EncounterEntryDef> entries)
    {
        var total = entries.Sum(e => Math.Max(1, e.Weight));
        var roll = Random.Next(0, total);
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

    /// <summary>Begins a battle with specific monsters.</summary>
    /// <param name="monsters">Opponents.</param>
    /// <param name="result">Result to annotate.</param>
    public void StartCombat(IEnumerable<MonsterInstance> monsters, StepResult result)
    {
        Combat = new CombatEngine(Rules, Random, State, monsters) { MagicSuppressed = () => IsAntiMagicHere };
        if (State.MapId == World.Depths.MapId && World.Depths.ScalePercent(State.Depth) is var pct and > 100)
        {
            foreach (var m in Combat.Monsters)
            {
                m.ScaleHp(pct);
                m.DamagePercent = m.DamagePercent * pct / 100;
            }
            Combat.RewardPercent = pct;
        }
        _deadBeforeCombat = State.Party.Where(c => c.Has(Condition.Dead)).ToHashSet();
        _combatFlag = null;
        result.CombatStarted = true;
        var names = Combat.Monsters.GroupBy(m => m.Def).Select(g => g.Count() == 1 ? g.Key.NameWithArticle : $"{g.Count()} {g.Key.PluralName}");
        result.Messages.Add(new($"Encounter! The party faces {string.Join(", ", names)}.", MessageKind.Bad, "roar"));
    }

    /// <summary>Closes the current battle, distributing rewards on victory.</summary>
    /// <returns>Narration and the final outcome.</returns>
    public (CombatOutcome Outcome, List<GameMessage> Messages) EndCombat()
    {
        var log = new List<GameMessage>();
        var combat = Combat;
        if (combat is null)
        {
            return (CombatOutcome.Ongoing, log);
        }
        foreach (var m in combat.Monsters.Where(m => m.IsDead))
        {
            State.KnownMonsters.Add(m.Def.Id);
            State.Kills[m.Def.Id] = State.Kills.GetValueOrDefault(m.Def.Id) + 1;
        }
        State.Count(Chronicle.Keys.Deaths, State.Party.Count(c => c.Has(Condition.Dead) && !_deadBeforeCombat.Contains(c)));
        if (combat.Outcome == CombatOutcome.Victory)
        {
            State.Count(Chronicle.Keys.BattlesWon);
            if (IsNightOutside)
            {
                State.Count(Chronicle.Keys.NightWins);
            }
        }
        else if (combat.Outcome == CombatOutcome.Fled)
        {
            State.Count(Chronicle.Keys.BattlesFled);
        }
        if (combat.Outcome == CombatOutcome.Victory && combat.Rewards is { } r)
        {
            State.Count(Chronicle.Keys.GoldFound, r.Gold);
            if (r.Gold > 0)
            {
                State.Gold += r.Gold;
                log.Add(new($"The party collects {r.Gold} gold.", MessageKind.Loot, "coins"));
            }
            if (r.Gems > 0)
            {
                State.Gems += r.Gems;
                log.Add(new($"The party finds {r.Gems} gem{(r.Gems > 1 ? "s" : "")}.", MessageKind.Loot));
            }
            foreach (var item in r.Items)
            {
                GiveItem(item, log);
            }
            if (r.Experience > 0)
            {
                AwardXp(r.Experience, log);
            }
            if (_combatEventKey is not null)
            {
                State.CompletedEvents.Add(_combatEventKey);
            }
            if (_combatFlag is not null)
            {
                State.Flags.Add(_combatFlag);
            }
        }
        combat.Finish();
        _combatEventKey = null;
        _combatFlag = null;
        Combat = null;
        return (combat.Outcome, log);
    }
}
