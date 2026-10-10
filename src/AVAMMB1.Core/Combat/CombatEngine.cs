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
    private readonly Dictionary<Character, Character> _guardedBy = new();
    private readonly HashSet<Character> _laidHands = new();
    private readonly Dictionary<Character, int> _aimedInRound = new();
    private int _turn;

    /// <summary>Experience and gold in percent of normal (the deeper levels of the Depths Below pay more).</summary>
    public int RewardPercent { get; set; } = 100;

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
        foreach (var m in Monsters.Where(m => m.Elite))
        {
            m.Label = "Elite " + m.Label;
        }
        if (state.Difficulty != Difficulty.Normal)
        {
            foreach (var m in Monsters)
            {
                m.ScaleHp(m.Def.Boss ? DifficultyRules.BossHp(state.Difficulty) : DifficultyRules.MonsterHp(state.Difficulty));
                m.DamagePercent = DifficultyRules.MonsterDamage(state.Difficulty);
            }
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

    /// <summary>Returns true when the battle takes place where magic is suppressed (affects both sides).</summary>
    public Func<bool>? MagicSuppressed
    {
        get => _spells.Suppressed;
        set => _spells.Suppressed = value;
    }

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
        else if (_state.Available(null) < cost)
        {
            log.Add(new($"They demand {cost} gold, but the party cannot pay.", MessageKind.Bad));
        }
        else if (_rng.Chance(80))
        {
            _state.TryPay(cost);
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
            CombatActionKind.Guard => Guard(c, action.Ally, log),
            CombatActionKind.LayOnHands => LayOnHands(c, action.Ally, log),
            CombatActionKind.AimedShot => AimedShot(c, action.Target, log),
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
        _guardedBy.Clear();
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
        // Paralysis wears off for characters too (more slowly than for monsters), so a long fight
        // against a paralyzing foe is a war of attrition rather than a guaranteed loss.
        foreach (var c in _state.Party.Where(c => c.IsAlive && c.Has(Condition.Paralyzed)))
        {
            if (_rng.Chance(20))
            {
                c.Conditions &= ~Condition.Paralyzed;
                log.Add(new($"{c.Name} can move again.", MessageKind.Good));
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

    /// <summary>Extra loot an elite may carry, by its level.</summary>
    /// <param name="level">Monster level.</param>
    public static IReadOnlyList<string> EliteLoot(int level) => level switch
    {
        <= 3 => ["potion_healing", "potion_cure", "scroll_light", "oil_flask"],
        <= 7 => ["potion_vigor", "potion_mana", "ring_protection", "amulet_insight", "scroll_fire", "oil_flask"],
        <= 11 => ["potion_vigor", "ring_might", "wand_lightning", "scroll_recall", "lotus_amulet"],
        _ => ["potion_vigor", "ring_heartfire", "scarab_amulet", "mithril_coat"],
    };

    private CombatRewards ComputeRewards()
    {
        var killed = Monsters.Where(m => m.IsDead).ToList();
        var xp = killed.Sum(m => m.Def.Xp * (m.Elite ? 3 : 1)) * DifficultyRules.Experience(_state.Difficulty) / 100 * RewardPercent / 100;
        var gold = killed.Sum(m => Math.Max(0, m.Def.Gold.Roll(_rng)) * (m.Elite ? 3 : 1)) * DifficultyRules.Gold(_state.Difficulty) / 100 * RewardPercent / 100;
        var gems = killed.Count(m => m.Def.Level >= 3 && _rng.Chance(10));
        var items = new List<ItemInstance>();
        foreach (var m in killed.Where(m => m.Elite && _rng.Chance(50)))
        {
            var pool = EliteLoot(m.Def.Level).Where(_rules.Content.Items.ContainsKey).ToList();
            if (pool.Count > 0)
            {
                var id = pool[_rng.Next(0, pool.Count)];
                items.Add(new ItemInstance(id, _rules.Content.Item(id).Charges));
            }
        }
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
            if (Rulebook.IsHit(_rng.Die(20), bonus, m.ArmorClass))
            {
                hits++;
                damage += _rules.RollMeleeDamage(c, _rng);
            }
        }
        var sneak = IsSneakAttack(c);
        ReportAttack(c.Name, m, hits, attacks, sneak ? damage * 2 : damage, sneak ? "strikes from the shadows at" : "attacks", log);
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
        var hit = Rulebook.IsHit(_rng.Die(20), _rules.MissileAttackBonus(c) + HitBuff, m.ArmorClass);
        var sneak = IsSneakAttack(c);
        var dmg = hit ? _rules.RollMissileDamage(c, _rng) * (sneak ? 2 : 1) : 0;
        ReportAttack(c.Name, m, hit ? 1 : 0, 1, dmg, sneak ? "shoots from the shadows at" : "shoots at", log);
        return true;
    }

    /// <summary>Robbers' sneak attack: double damage on the first round.</summary>
    private bool IsSneakAttack(Character c) => Round == 1 && _rules.HasAbility(c, ClassAbility.SneakAttack);

    private bool AimedShot(Character c, int target, List<GameMessage> log)
    {
        if (!_rules.HasAbility(c, ClassAbility.AimedShot))
        {
            return false;
        }
        if (!_rules.HasMissileWeapon(c))
        {
            log.Add(new($"{c.Name} has no missile weapon to aim.", MessageKind.Info));
            return false;
        }
        if (!CanAim(c))
        {
            log.Add(new($"{c.Name} needs a round to steady their aim again.", MessageKind.Info));
            return false;
        }
        var m = ResolveTarget(target, meleeOnly: false);
        if (m is null)
        {
            return false;
        }
        _aimedInRound[c] = Round;
        var hit = Rulebook.IsHit(_rng.Die(20), _rules.MissileAttackBonus(c) + HitBuff + 4, m.ArmorClass);
        ReportAttack(c.Name, m, hit ? 1 : 0, 1, hit ? _rules.RollMissileDamage(c, _rng) * 2 : 0, "takes careful aim at", log);
        return true;
    }

    private bool Guard(Character c, int ally, List<GameMessage> log)
    {
        if (!_rules.HasAbility(c, ClassAbility.Guard) || ally < 0 || ally >= _state.Party.Count)
        {
            return false;
        }
        var target = _state.Party[ally];
        if (ReferenceEquals(target, c) || !target.IsAlive)
        {
            log.Add(new($"{c.Name} can only guard a living companion.", MessageKind.Info));
            return false;
        }
        _guardedBy[target] = c;
        _blocking.Add(c);
        log.Add(new($"{c.Name} raises a shield before {target.Name}.", MessageKind.Combat, "guard"));
        return true;
    }

    private bool LayOnHands(Character c, int ally, List<GameMessage> log)
    {
        if (!_rules.HasAbility(c, ClassAbility.LayOnHands) || ally < 0 || ally >= _state.Party.Count)
        {
            return false;
        }
        if (_laidHands.Contains(c))
        {
            log.Add(new($"{c.Name} has already laid on hands this battle.", MessageKind.Info));
            return false;
        }
        var target = _state.Party[ally];
        if (!target.IsAlive)
        {
            log.Add(new($"{target.Name} is beyond a paladin's touch.", MessageKind.Info));
            return false;
        }
        _laidHands.Add(c);
        var amount = 3 * c.Level + 5;
        Rulebook.Heal(target, amount);
        target.Conditions &= ~Condition.Poisoned;
        log.Add(new($"{c.Name} lays hands on {target.Name}, healing {amount} and drawing out any poison.", MessageKind.Good, "heal"));
        return true;
    }

    /// <summary>Whether an archer can take an aimed shot now (not two rounds running).</summary>
    /// <param name="c">Character.</param>
    public bool CanAim(Character c) =>
        _rules.HasAbility(c, ClassAbility.AimedShot) && _rules.HasMissileWeapon(c) &&
        (!_aimedInRound.TryGetValue(c, out var r) || r < Round - 1);

    /// <summary>Whether a paladin has already laid on hands this battle.</summary>
    /// <param name="c">Character.</param>
    public bool HasLaidHands(Character c) => _laidHands.Contains(c);

    /// <summary>A knight guarding the target steps in front of the blow.</summary>
    private Character? Redirect(Character? target, List<GameMessage> log)
    {
        if (target is not null && _guardedBy.TryGetValue(target, out var guard) && guard.IsAlive && guard.CanAct)
        {
            log.Add(new($"{guard.Name} steps in front of {target.Name}!", MessageKind.Combat));
            return guard;
        }
        return target;
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
        foreach (var ability in MagicSuppressed?.Invoke() == true ? [] : m.Def.Abilities)
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
            var target = Redirect(ChooseTarget(attack.Ranged, m.Def.Smart), log);
            if (target is null)
            {
                return;
            }
            if (!Rulebook.IsHit(_rng.Die(20), m.Def.Level + 1, PartyArmor(target)))
            {
                log.Add(new($"{m.Label} {attack.Verb} at {target.Name} but misses.", MessageKind.Combat, "miss"));
                continue;
            }
            var dmg = m.ScaleDamage(Math.Max(1, attack.Damage.Roll(_rng)));
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
            : Redirect(ChooseTarget(true, m.Def.Smart), log) is { } single ? [single] : new List<Character>();
        if (targets.Count == 0)
        {
            return false;
        }
        log.Add(new($"{m.Label} uses {a.Name}!", MessageKind.Bad, a.Element == Element.Fire ? "fire" : "spell")
        {
            Effect = a.Element == Element.Physical ? null : a.Element.ToString().ToLowerInvariant(),
        });
        foreach (var t in targets)
        {
            if (a.Damage.Max > 0)
            {
                var dmg = m.ScaleDamage(a.Damage.Roll(_rng));
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
