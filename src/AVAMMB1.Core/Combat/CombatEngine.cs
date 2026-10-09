using AVAMMB1.Core.Characters;
using AVAMMB1.Core.Content;
using AVAMMB1.Core.Dice;
using AVAMMB1.Core.Items;
using AVAMMB1.Core.Magic;
using AVAMMB1.Core.Rules;
using AVAMMB1.Core.Session;

namespace AVAMMB1.Core.Combat;

/// <summary>
/// Turn-based battle between the party and a group of monsters.
/// Each round every combatant acts in initiative order (Speed + d6). The engine pauses whenever a
/// party member needs a decision; call <see cref="Act"/> to supply it.
/// </summary>
public sealed class CombatEngine
{
    /// <summary>Number of combatants on each side that can fight hand-to-hand.</summary>
    public const int FrontRank = 3;

    private readonly Rulebook _rules;
    private readonly IRandomSource _rng;
    private readonly GameState _state;
    private readonly SpellCaster _spells;
    private readonly List<object> _order = new();
    private readonly HashSet<Character> _blocking = new();
    private int _turn;

    /// <summary>Creates a battle.</summary>
    /// <param name="rules">Rulebook.</param>
    /// <param name="rng">Random source.</param>
    /// <param name="state">Game state (party, gold).</param>
    /// <param name="monsters">The opposing monsters.</param>
    public CombatEngine(Rulebook rules, IRandomSource rng, GameState state, IEnumerable<MonsterInstance> monsters)
    {
        _rules = rules;
        _rng = rng;
        _state = state;
        _spells = new SpellCaster(rules, rng);
        Monsters = monsters.ToList();
        var counts = Monsters.GroupBy(m => m.Def.Id).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();
        var seen = new Dictionary<string, int>();
        foreach (var m in Monsters.Where(m => counts.Contains(m.Def.Id)))
        {
            seen[m.Def.Id] = seen.GetValueOrDefault(m.Def.Id) + 1;
            m.Label = $"{m.Def.Name} #{seen[m.Def.Id]}";
        }
    }

    /// <summary>Creates monster instances for a definition, rolling hit points.</summary>
    /// <param name="def">Monster template.</param>
    /// <param name="count">How many.</param>
    /// <param name="rng">Random source.</param>
    public static IEnumerable<MonsterInstance> Spawn(MonsterDef def, int count, IRandomSource rng) =>
        Enumerable.Range(0, Math.Max(1, count)).Select(_ => new MonsterInstance(def, def.HitPoints.Roll(rng)));

    /// <summary>The monsters (including dead and fled ones).</summary>
    public List<MonsterInstance> Monsters { get; }

    /// <summary>The party.</summary>
    public IReadOnlyList<Character> Party => _state.Party;

    /// <summary>Current round number (1-based).</summary>
    public int Round { get; private set; }

    /// <summary>Current outcome.</summary>
    public CombatOutcome Outcome { get; private set; }

    /// <summary>The party member awaiting a decision, if any.</summary>
    public Character? ActiveCharacter { get; private set; }

    /// <summary>Raised when a monster takes an action (attack or ability), e.g. to animate it.</summary>
    public event Action<MonsterInstance>? MonsterActed;

    /// <summary>Party-wide armor class bonus from spells.</summary>
    public int ArmorBuff { get; set; }

    /// <summary>Party-wide to-hit bonus from spells.</summary>
    public int HitBuff { get; set; }

    /// <summary>Rewards, available once <see cref="Outcome"/> is <see cref="CombatOutcome.Victory"/>.</summary>
    public CombatRewards? Rewards { get; private set; }

    /// <summary>Monsters still fighting.</summary>
    public IEnumerable<MonsterInstance> ActiveMonsters => Monsters.Where(m => m.IsActive);

    /// <summary>Monsters that can be reached in melee (first three still fighting).</summary>
    public IEnumerable<MonsterInstance> MeleeTargets => ActiveMonsters.Take(FrontRank);

    private IEnumerable<Character> Standing => _state.Party.Where(c => c.IsAlive && !c.Has(Condition.Unconscious));

    /// <summary>Whether a party member is in the front rank (first three standing members).</summary>
    /// <param name="c">Character.</param>
    public bool IsInFrontRank(Character c) => Standing.Take(FrontRank).Contains(c);

    /// <summary>Total gold demanded as a bribe, or <c>null</c> if the monsters cannot be bribed.</summary>
    public int? BribeCost =>
        ActiveMonsters.All(m => m.Def.Bribable) ? ActiveMonsters.Sum(m => m.Def.Level * 15 + 10) : null;

    /// <summary>Chance (percent) that the party escapes when running.</summary>
    public int RunChance
    {
        get
        {
            var party = Standing.Select(c => _rules.Stat(c, Stat.Speed)).DefaultIfEmpty(10).Average();
            var mons = ActiveMonsters.Select(m => m.Def.Speed).DefaultIfEmpty(10).Average();
            return Math.Clamp(50 + (int)((party - mons) * 4), 10, 90);
        }
    }

    /// <summary>Attempts to pay off the monsters before fighting.</summary>
    /// <returns>Messages describing the result.</returns>
    public IReadOnlyList<GameMessage> TryBribe()
    {
        var log = new List<GameMessage>();
        if (BribeCost is not { } cost)
        {
            log.Add(new("These creatures have no interest in gold!", MessageKind.Bad));
        }
        else if (_state.Gold < cost)
        {
            log.Add(new($"They demand {cost} gold, but the party cannot pay.", MessageKind.Bad));
        }
        else if (_rng.Chance(80))
        {
            _state.Gold -= cost;
            Outcome = CombatOutcome.Bribed;
            log.Add(new($"The party pays {cost} gold. The monsters leave.", MessageKind.Info, "coins"));
            return log;
        }
        else
        {
            log.Add(new("The monsters take offense at the offer!", MessageKind.Bad, "roar"));
        }
        log.AddRange(Advance());
        return log;
    }

    /// <summary>Runs the battle until a party member must decide or the battle ends.</summary>
    /// <returns>Narration produced.</returns>
    public IReadOnlyList<GameMessage> Advance()
    {
        var log = new List<GameMessage>();
        ActiveCharacter = null;
        var guard = 0;
        while (CheckEnd(log) == CombatOutcome.Ongoing && guard++ < 10000)
        {
            if (_turn >= _order.Count)
            {
                StartRound(log);
                continue;
            }
            var actor = _order[_turn];
            if (actor is Character c)
            {
                if (c.CanAct)
                {
                    _blocking.Remove(c);
                    ActiveCharacter = c;
                    return log;
                }
                _turn++;
            }
            else if (actor is MonsterInstance m)
            {
                _turn++;
                MonsterTurn(m, log);
            }
        }
        return log;
    }

    /// <summary>Performs the active character's chosen action, then advances.</summary>
    /// <param name="action">The action.</param>
    /// <returns>Narration produced.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no character is awaiting input.</exception>
    public IReadOnlyList<GameMessage> Act(CombatAction action)
    {
        var c = ActiveCharacter ?? throw new InvalidOperationException("No party member is waiting to act.");
        var log = new List<GameMessage>();
        var consumed = action.Kind switch
        {
            CombatActionKind.Attack => Melee(c, action.Target, log),
            CombatActionKind.Shoot => Shoot(c, action.Target, log),
            CombatActionKind.Cast => Cast(c, action, log),
            CombatActionKind.UseItem => UseItem(c, action, log),
            CombatActionKind.Block => Block(c, log),
            CombatActionKind.Run => Run(log),
            _ => false,
        };
        if (!consumed)
        {
            return log; // invalid choice: same character chooses again
        }
        if (Outcome == CombatOutcome.Ongoing)
        {
            _turn++;
            log.AddRange(Advance());
        }
        else
        {
            ActiveCharacter = null;
        }
        return log;
    }

    private void StartRound(List<GameMessage> log)
    {
        Round++;
        _turn = 0;
        _order.Clear();
        var entries = new List<(object Actor, int Init)>();
        foreach (var c in _state.Party.Where(c => c.IsAlive))
        {
            entries.Add((c, _rules.Stat(c, Stat.Speed) + _rng.Die(6)));
        }
        foreach (var m in ActiveMonsters)
        {
            entries.Add((m, m.Def.Speed + _rng.Die(6)));
        }
        _order.AddRange(entries.OrderByDescending(e => e.Init).Select(e => e.Actor));

        foreach (var m in ActiveMonsters)
        {
            if (m.Def.Regenerates > 0 && m.Hp < m.MaxHp)
            {
                m.Hp = Math.Min(m.MaxHp, m.Hp + m.Def.Regenerates);
            }
            if ((m.Conditions & Condition.Paralyzed) != 0 && _rng.Chance(30))
            {
                m.Conditions &= ~Condition.Paralyzed;
                log.Add(new($"{m.Label} can move again.", MessageKind.Combat));
            }
            if ((m.Conditions & Condition.Asleep) != 0 && _rng.Chance(20))
            {
                m.Conditions &= ~Condition.Asleep;
                log.Add(new($"{m.Label} wakes up.", MessageKind.Combat));
            }
        }
        foreach (var c in _state.Party.Where(c => c.IsAlive && c.Has(Condition.Poisoned)))
        {
            if (_rules.ApplyDamage(c, 1))
            {
                log.Add(new($"{c.Name} succumbs to poison!", MessageKind.Bad));
            }
        }
    }

    private CombatOutcome CheckEnd(List<GameMessage> log)
    {
        if (Outcome != CombatOutcome.Ongoing)
        {
            return Outcome;
        }
        if (!ActiveMonsters.Any())
        {
            Outcome = CombatOutcome.Victory;
            Rewards = ComputeRewards();
            log.Add(new("The battle is won!", MessageKind.Good));
        }
        else if (!_state.Party.Any(c => c.IsAlive && (c.Conditions & (Condition.Unconscious | Condition.Paralyzed)) == 0))
        {
            Outcome = CombatOutcome.Defeat;
            log.Add(new("The party has fallen...", MessageKind.Bad));
        }
        return Outcome;
    }

    private CombatRewards ComputeRewards()
    {
        var killed = Monsters.Where(m => m.IsDead).ToList();
        var xp = killed.Sum(m => m.Def.Xp);
        var gold = killed.Sum(m => Math.Max(0, m.Def.Gold.Roll(_rng)));
        var gems = killed.Count(m => m.Def.Level >= 3 && _rng.Chance(10));
        var items = new List<ItemInstance>();
        foreach (var m in killed)
        {
            foreach (var d in m.Def.Drops)
            {
                if (_rng.Chance(d.Chance))
                {
                    items.Add(new ItemInstance(d.Item, _rules.Content.Item(d.Item).Charges));
                }
            }
        }
        return new CombatRewards(xp, gold, gems, items);
    }

    private MonsterInstance? ResolveTarget(int index, bool meleeOnly)
    {
        var pool = (meleeOnly ? MeleeTargets : ActiveMonsters).ToList();
        if (index >= 0 && index < Monsters.Count && pool.Contains(Monsters[index]))
        {
            return Monsters[index];
        }
        return pool.FirstOrDefault();
    }

    private bool Melee(Character c, int target, List<GameMessage> log)
    {
        if (!IsInFrontRank(c))
        {
            log.Add(new($"{c.Name} is too far back to fight hand-to-hand.", MessageKind.Info));
            return false;
        }
        var m = ResolveTarget(target, meleeOnly: true);
        if (m is null)
        {
            return false;
        }
        var attacks = _rules.AttacksPerRound(c);
        var bonus = _rules.MeleeAttackBonus(c) + HitBuff;
        var hits = 0;
        var damage = 0;
        for (var i = 0; i < attacks && m.IsActive; i++)
        {
            if (Rulebook.IsHit(_rng.Die(20), bonus, m.Def.ArmorClass))
            {
                hits++;
                damage += _rules.RollMeleeDamage(c, _rng);
            }
        }
        ReportAttack(c.Name, m, hits, attacks, damage, "attacks", log);
        return true;
    }

    private bool Shoot(Character c, int target, List<GameMessage> log)
    {
        if (!_rules.HasMissileWeapon(c))
        {
            log.Add(new($"{c.Name} has no missile weapon.", MessageKind.Info));
            return false;
        }
        var m = ResolveTarget(target, meleeOnly: false);
        if (m is null)
        {
            return false;
        }
        var hit = Rulebook.IsHit(_rng.Die(20), _rules.MissileAttackBonus(c) + HitBuff, m.Def.ArmorClass);
        ReportAttack(c.Name, m, hit ? 1 : 0, 1, hit ? _rules.RollMissileDamage(c, _rng) : 0, "shoots at", log);
        return true;
    }

    private void ReportAttack(string who, MonsterInstance m, int hits, int attempts, int damage, string verb, List<GameMessage> log)
    {
        if (hits == 0)
        {
            log.Add(new($"{who} {verb} {m.Label} and misses.", MessageKind.Combat, "miss"));
            return;
        }
        var times = attempts > 1 ? $" {hits} time{(hits > 1 ? "s" : "")}" : "";
        log.Add(new($"{who} {verb} {m.Label}, hitting{times} for {damage} damage.", MessageKind.Combat, "hit"));
        DamageMonster(m, damage, Element.Physical, log);
    }

    /// <summary>Applies damage to a monster with resistances, waking it and reporting death.</summary>
    /// <param name="m">Monster.</param>
    /// <param name="amount">Raw damage.</param>
    /// <param name="element">Damage element.</param>
    /// <param name="log">Log to append to.</param>
    /// <returns>Damage actually dealt.</returns>
    public int DamageMonster(MonsterInstance m, int amount, Element element, List<GameMessage> log)
    {
        if (!m.IsActive || amount <= 0)
        {
            return 0;
        }
        if (m.Def.Resistances.TryGetValue(element, out var res))
        {
            amount = amount * (100 - Math.Clamp(res, -100, 100)) / 100;
        }
        if (element == Element.Holy && m.Def.Undead)
        {
            amount *= 2;
        }
        amount = Math.Max(0, amount);
        m.Hp -= amount;
        m.Conditions &= ~Condition.Asleep;
        if (m.Hp <= 0)
        {
            log.Add(new($"{m.Label} is slain!", MessageKind.Good, "monster_die"));
        }
        return amount;
    }

    /// <summary>Tries to inflict a condition on a monster (saving throw based on its level).</summary>
    /// <param name="m">Monster.</param>
    /// <param name="condition">Condition.</param>
    /// <param name="casterLevel">Caster level.</param>
    /// <param name="log">Log to append to.</param>
    public bool InflictMonster(MonsterInstance m, Condition condition, int casterLevel, List<GameMessage> log)
    {
        if (!m.IsActive)
        {
            return false;
        }
        if (m.Def.Undead && condition.HasFlag(Condition.Asleep))
        {
            log.Add(new($"{m.Label} never sleeps.", MessageKind.Combat));
            return false;
        }
        var chance = Math.Clamp(60 + (casterLevel - m.Def.Level) * 8, 10, 95);
        if (_rng.Chance(chance))
        {
            m.Conditions |= condition;
            log.Add(new($"{m.Label} is {(condition.HasFlag(Condition.Asleep) ? "put to sleep" : "held fast")}!", MessageKind.Combat));
            return true;
        }
        log.Add(new($"{m.Label} resists.", MessageKind.Combat));
        return false;
    }

    private bool Cast(Character c, CombatAction action, List<GameMessage> log)
    {
        if (action.SpellId is null || !_rules.Content.Spells.TryGetValue(action.SpellId, out var spell))
        {
            return false;
        }
        var reason = _spells.CanCast(c, spell, inCombat: true);
        if (reason is not null)
        {
            log.Add(new(reason, MessageKind.Info));
            return false;
        }
        var target = action.Target >= 0 && action.Target < Monsters.Count ? Monsters[action.Target] : null;
        var result = _spells.Cast(c, spell, _state, this, action.Ally, target);
        log.AddRange(result.Messages);
        return result.Success;
    }

    private bool UseItem(Character c, CombatAction action, List<GameMessage> log)
    {
        var target = action.Target >= 0 && action.Target < Monsters.Count ? Monsters[action.Target] : null;
        var result = _spells.UseItem(c, action.ItemIndex, _state, this, action.Ally, target);
        log.AddRange(result.Messages);
        return result.Success;
    }

    private bool Block(Character c, List<GameMessage> log)
    {
        _blocking.Add(c);
        log.Add(new($"{c.Name} raises a guard.", MessageKind.Combat));
        return true;
    }

    private bool Run(List<GameMessage> log)
    {
        if (_rng.Chance(RunChance))
        {
            Outcome = CombatOutcome.Fled;
            log.Add(new("The party flees!", MessageKind.Info, "step"));
        }
        else
        {
            log.Add(new("The party fails to escape!", MessageKind.Bad));
            // The rest of the party forfeits its actions this round; monsters still act.
            for (var i = _order.Count - 1; i > _turn; i--)
            {
                if (_order[i] is Character)
                {
                    _order.RemoveAt(i);
                }
            }
        }
        return true;
    }

    private int PartyArmor(Character c) => _rules.ArmorClass(c) + ArmorBuff + (_blocking.Contains(c) ? 4 : 0);

    private void MonsterTurn(MonsterInstance m, List<GameMessage> log)
    {
        if (!m.CanAct)
        {
            return;
        }
        if (m.Def.Cowardly && m.Hp < m.MaxHp / 4 && _rng.Chance(35))
        {
            m.Fled = true;
            log.Add(new($"{m.Label} flees in terror!", MessageKind.Combat));
            return;
        }
        foreach (var ability in m.Def.Abilities)
        {
            if (_rng.Chance(ability.Chance) && UseAbility(m, ability, log))
            {
                MonsterActed?.Invoke(m);
                return;
            }
        }
        var inFront = MeleeTargets.Contains(m);
        if (m.Def.Attacks.Any(a => a.Ranged || inFront))
        {
            MonsterActed?.Invoke(m);
        }
        foreach (var attack in m.Def.Attacks)
        {
            if (!attack.Ranged && !inFront)
            {
                continue;
            }
            var target = ChooseTarget(attack.Ranged, m.Def.Smart);
            if (target is null)
            {
                return;
            }
            if (!Rulebook.IsHit(_rng.Die(20), m.Def.Level + 1, PartyArmor(target)))
            {
                log.Add(new($"{m.Label} {attack.Verb} at {target.Name} but misses.", MessageKind.Combat, "miss"));
                continue;
            }
            var dmg = Math.Max(1, attack.Damage.Roll(_rng));
            dmg = dmg * (100 - _rules.Resistance(target, attack.Element)) / 100;
            log.Add(new($"{m.Label} {attack.Verb} {target.Name} for {dmg} damage.", MessageKind.Bad, "party_hurt"));
            HurtCharacter(target, dmg, log);
            if (attack.Inflicts != Condition.None && target.IsAlive && _rng.Chance(attack.InflictChance) &&
                !_rules.SavingThrow(target, m.Def.Level, _rng))
            {
                target.Conditions |= attack.Inflicts;
                log.Add(new($"{target.Name} is {attack.Inflicts.ToString().ToLowerInvariant()}!", MessageKind.Bad));
            }
        }
    }

    private bool UseAbility(MonsterInstance m, MonsterAbilityDef a, List<GameMessage> log)
    {
        if (a.SelfHeal.Max > 0)
        {
            if (m.Hp >= m.MaxHp * 3 / 4)
            {
                return false;
            }
            var healed = Math.Min(m.MaxHp - m.Hp, a.SelfHeal.Roll(_rng));
            m.Hp += healed;
            log.Add(new($"{m.Label} uses {a.Name} and recovers {healed} HP.", MessageKind.Combat, "heal"));
            return true;
        }
        var targets = a.AllTargets
            ? Standing.ToList()
            : ChooseTarget(true, m.Def.Smart) is { } single ? [single] : new List<Character>();
        if (targets.Count == 0)
        {
            return false;
        }
        log.Add(new($"{m.Label} uses {a.Name}!", MessageKind.Bad, a.Element == Element.Fire ? "fire" : "spell"));
        foreach (var t in targets)
        {
            if (a.Damage.Max > 0)
            {
                var dmg = a.Damage.Roll(_rng);
                if (_rules.SavingThrow(t, m.Def.Level, _rng))
                {
                    dmg /= 2;
                }
                dmg = dmg * (100 - _rules.Resistance(t, a.Element)) / 100;
                log.Add(new($"{t.Name} takes {dmg} damage.", MessageKind.Bad));
                HurtCharacter(t, dmg, log);
            }
            if (a.Inflicts != Condition.None && t.IsAlive && !_rules.SavingThrow(t, m.Def.Level, _rng))
            {
                t.Conditions |= a.Inflicts;
                log.Add(new($"{t.Name} is {a.Inflicts.ToString().ToLowerInvariant()}!", MessageKind.Bad));
            }
        }
        return true;
    }

    private void HurtCharacter(Character c, int dmg, List<GameMessage> log)
    {
        if (_rules.ApplyDamage(c, dmg))
        {
            log.Add(new(c.Has(Condition.Dead) ? $"{c.Name} has been killed!" : $"{c.Name} falls unconscious!", MessageKind.Bad));
        }
    }

    private Character? ChooseTarget(bool ranged, bool smart)
    {
        var pool = (ranged ? Standing : Standing.Take(FrontRank)).ToList();
        if (pool.Count == 0)
        {
            return null;
        }
        return smart ? pool.OrderBy(c => c.Hp).First() : _rng.Pick(pool);
    }

    /// <summary>Cleans up after the battle: wakes sleepers.</summary>
    public void Finish()
    {
        foreach (var c in _state.Party)
        {
            c.Conditions &= ~Condition.Asleep;
        }
        ActiveCharacter = null;
    }
}
