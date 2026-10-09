using AVAMMB1.Core.Characters;
using AVAMMB1.Core.Combat;
using AVAMMB1.Core.Content;
using AVAMMB1.Core.Dice;
using AVAMMB1.Core.Rules;
using AVAMMB1.Core.Session;

namespace AVAMMB1.Core.Magic;

/// <summary>Outcome of casting a spell or using an item.</summary>
/// <param name="Success">Whether the action happened (and consumed the turn / resources).</param>
/// <param name="Messages">Narration.</param>
/// <param name="Teleported">Whether the party was moved (Recall).</param>
public sealed record SpellResult(bool Success, IReadOnlyList<GameMessage> Messages, bool Teleported = false);

/// <summary>Resolves spell effects both in and out of combat, and item use.</summary>
/// <param name="rules">Rulebook.</param>
/// <param name="rng">Random source.</param>
public sealed class SpellCaster(Rulebook rules, IRandomSource rng)
{
    /// <summary>Message shown when magic is suppressed.</summary>
    public const string SuppressedMessage = "The magic fizzles - something here smothers every spell.";

    /// <summary>Returns true while the party stands where magic does not work (anti-magic squares).</summary>
    public Func<bool>? Suppressed { get; set; }

    private bool IsSuppressed => Suppressed?.Invoke() == true;

    /// <summary>Returns why a character cannot cast a spell now, or <c>null</c> if they can.</summary>
    /// <param name="c">Caster.</param>
    /// <param name="spell">Spell.</param>
    /// <param name="inCombat">Whether a battle is in progress.</param>
    public string? CanCast(Character c, SpellDef spell, bool inCombat)
    {
        if (!c.CanAct)
        {
            return $"{c.Name} is in no condition to cast.";
        }
        if (c.Has(Condition.Silenced))
        {
            return $"{c.Name} has been silenced!";
        }
        if (IsSuppressed)
        {
            return SuppressedMessage;
        }
        if (!rules.KnownSpells(c).Contains(spell))
        {
            return $"{c.Name} does not know {spell.Name}.";
        }
        if (c.Sp < spell.Cost)
        {
            return $"{c.Name} lacks the spell points for {spell.Name}.";
        }
        if (inCombat && !spell.Combat)
        {
            return $"{spell.Name} cannot be cast in combat.";
        }
        if (!inCombat && !spell.Explore)
        {
            return $"{spell.Name} can only be cast in combat.";
        }
        return null;
    }

    /// <summary>Casts a spell, paying its cost.</summary>
    /// <param name="caster">Caster.</param>
    /// <param name="spell">Spell.</param>
    /// <param name="state">Game state.</param>
    /// <param name="combat">Battle in progress, or <c>null</c> when exploring.</param>
    /// <param name="ally">Party index of an ally target.</param>
    /// <param name="enemy">Enemy target.</param>
    public SpellResult Cast(Character caster, SpellDef spell, GameState state, CombatEngine? combat, int ally, MonsterInstance? enemy)
    {
        var reason = CanCast(caster, spell, combat is not null);
        if (reason is not null)
        {
            return new SpellResult(false, [new GameMessage(reason)]);
        }
        var log = new List<GameMessage> { new($"{caster.Name} casts {spell.Name}.", MessageKind.Combat, spell.Element == Element.Fire ? "fire" : "spell") };
        var result = Apply(spell, Math.Max(1, rules.CasterLevel(caster)), state, combat, ally, enemy, log);
        if (result.Success)
        {
            caster.Sp -= spell.Cost;
        }
        return result;
    }

    /// <summary>Uses an item from a character's backpack (potions, scrolls, wands, food, torches).</summary>
    /// <param name="user">The character using the item.</param>
    /// <param name="backpackIndex">Index in the backpack.</param>
    /// <param name="state">Game state.</param>
    /// <param name="combat">Battle in progress, or <c>null</c>.</param>
    /// <param name="ally">Party index of an ally target.</param>
    /// <param name="enemy">Enemy target.</param>
    public SpellResult UseItem(Character user, int backpackIndex, GameState state, CombatEngine? combat, int ally, MonsterInstance? enemy)
    {
        if (backpackIndex < 0 || backpackIndex >= user.Backpack.Count)
        {
            return new SpellResult(false, [new GameMessage("No such item.")]);
        }
        if (!user.CanAct)
        {
            return new SpellResult(false, [new GameMessage($"{user.Name} cannot do that now.")]);
        }
        var inst = user.Backpack[backpackIndex];
        var def = rules.Def(inst);
        var log = new List<GameMessage>();
        SpellResult result;
        switch (def.Kind)
        {
            case ItemKind.Food when combat is null:
                {
                    var target = ally >= 0 && ally < state.Party.Count ? state.Party[ally] : user;
                    target.Food = Math.Min(rules.Content.Config.MaxFood, target.Food + Math.Max(1, def.FoodUnits));
                    log.Add(new($"{target.Name} packs away {def.Name}. Food: {target.Food}.", MessageKind.Good));
                    result = new SpellResult(true, log);
                    break;
                }
            case ItemKind.Tome when combat is null:
                {
                    if (def.TeachSpell is null || !rules.Content.Spells.TryGetValue(def.TeachSpell, out var taught))
                    {
                        return new SpellResult(false, [new GameMessage($"{def.Name} is written in no language anyone can read.")]);
                    }
                    var school = rules.Content.Class(user.Class).SpellSchool;
                    if (school != taught.School)
                    {
                        return new SpellResult(false, [new GameMessage($"{user.Name} cannot make sense of {def.Name} - it holds {taught.School} magic.")]);
                    }
                    if (user.LearnedSpells.Contains(taught.Id))
                    {
                        return new SpellResult(false, [new GameMessage($"{user.Name} already knows {taught.Name}.")]);
                    }
                    if (rules.MaxSpellLevel(user) < taught.Level)
                    {
                        return new SpellResult(false, [new GameMessage($"{taught.Name} is a level {taught.Level} spell - beyond {user.Name}'s skill for now.")]);
                    }
                    user.LearnedSpells.Add(taught.Id);
                    log.Add(new($"{user.Name} studies {def.Name} and learns {taught.Name}! The tome crumbles to dust.", MessageKind.Good, "levelup"));
                    result = new SpellResult(true, log);
                    break;
                }
            case ItemKind.Torch when combat is null:
                state.AddLight(Math.Max(10, def.LightSteps), def.LightRadius > 0 ? def.LightRadius : 5);
                log.Add(new($"{user.Name} lights {def.Name}.", MessageKind.Good));
                result = new SpellResult(true, log);
                break;
            case ItemKind.Oil when combat is null:
                {
                    // Fill the chosen ally's lantern, else the user's, else the emptiest lantern in the party.
                    var holders = state.Party.Where(p => Items.Lanterns.Refillable(rules, p)).ToList();
                    var target = ally >= 0 && ally < state.Party.Count && holders.Contains(state.Party[ally]) ? state.Party[ally]
                        : holders.Contains(user) ? user
                        : holders.OrderBy(p => p.Equipment[EquipSlot.Light].Charges).FirstOrDefault();
                    if (target is null)
                    {
                        return new SpellResult(false, [new GameMessage("Nobody has a lantern equipped to fill.")]);
                    }
                    var lantern = target.Equipment[EquipSlot.Light];
                    var cap = rules.Def(lantern).FuelCapacity;
                    if (lantern.Charges >= cap)
                    {
                        return new SpellResult(false, [new GameMessage($"{target.Name}'s lantern is already full.")]);
                    }
                    lantern.Charges = Math.Min(cap, lantern.Charges + Math.Max(1, def.FuelAmount));
                    log.Add(new($"{user.Name} fills {(ReferenceEquals(target, user) ? "their" : target.Name + "'s")} lantern ({lantern.Charges}/{cap} steps of oil).", MessageKind.Good));
                    result = new SpellResult(true, log);
                    break;
                }
            default:
                if (def.UseSpell is null || !rules.Content.Spells.TryGetValue(def.UseSpell, out var spell))
                {
                    return new SpellResult(false, [new GameMessage($"{def.Name} cannot be used like that.")]);
                }
                if (IsSuppressed && def.Kind is ItemKind.Scroll or ItemKind.Wand)
                {
                    return new SpellResult(false, [new GameMessage(SuppressedMessage)]);
                }
                if (combat is not null && !spell.Combat || combat is null && !spell.Explore)
                {
                    return new SpellResult(false, [new GameMessage($"{def.Name} has no effect here.")]);
                }
                log.Add(new($"{user.Name} uses {def.Name}.", MessageKind.Combat, def.Kind == ItemKind.Potion ? "drink" : "spell"));
                result = Apply(spell, Math.Max(5, user.Level), state, combat, ally, enemy, log);
                break;
        }
        if (result.Success)
        {
            if (inst.Charges > 1)
            {
                inst.Charges--;
            }
            else
            {
                user.Backpack.RemoveAt(backpackIndex);
                if (inst.Charges == 1)
                {
                    log.Add(new($"{def.Name} crumbles to dust.", MessageKind.Info));
                }
            }
        }
        return result;
    }

    private SpellResult Apply(SpellDef spell, int power, GameState state, CombatEngine? combat, int ally, MonsterInstance? enemy, List<GameMessage> log)
    {
        int Amount() => Math.Max(0, spell.Amount.Roll(rng) + spell.PerLevel * power);
        var allyTarget = ally >= 0 && ally < state.Party.Count ? state.Party[ally] : null;
        var party = state.Party;

        switch (spell.Effect)
        {
            case EffectKind.Damage:
            case EffectKind.Inflict:
                {
                    if (combat is null)
                    {
                        return Fail("There is nothing to target here.", log);
                    }
                    var targets = EnemyTargets(spell.Target, combat, enemy);
                    if (spell.UndeadOnly)
                    {
                        targets = targets.Where(m => m.Def.Undead).ToList();
                    }
                    if (targets.Count == 0)
                    {
                        log.Add(new("The spell finds no valid target.", MessageKind.Info));
                        return new SpellResult(true, log);
                    }
                    foreach (var m in targets)
                    {
                        if (spell.Effect == EffectKind.Damage)
                        {
                            var idx = log.Count;
                            var dealt = combat.DamageMonster(m, Amount(), spell.Element, log);
                            log.Insert(idx, new($"{m.Label} takes {dealt} damage.", MessageKind.Combat, "hit"));
                        }
                        else
                        {
                            combat.InflictMonster(m, spell.Conditions, power, log);
                        }
                    }
                    return new SpellResult(true, log);
                }
            case EffectKind.Heal:
                {
                    var targets = spell.Target == TargetKind.Party ? party.Where(c => c.IsAlive).ToList() : Single(allyTarget);
                    if (targets.Count == 0)
                    {
                        return Fail("Choose a living party member.", log);
                    }
                    foreach (var t in targets)
                    {
                        var healed = Rulebook.Heal(t, Amount());
                        log.Add(new($"{t.Name} recovers {healed} HP.", MessageKind.Good, "heal"));
                    }
                    return new SpellResult(true, log);
                }
            case EffectKind.Cure:
                {
                    var curesStone = spell.Conditions.HasFlag(Condition.Stoned);
                    var targets = spell.Target == TargetKind.Party
                        ? party.Where(c => c.IsAlive || (curesStone && c.Has(Condition.Stoned) && !c.Has(Condition.Dead))).ToList()
                        : allyTarget is not null && curesStone && allyTarget.Has(Condition.Stoned) && !allyTarget.Has(Condition.Dead) ? [allyTarget] : Single(allyTarget);
                    if (targets.Count == 0)
                    {
                        return Fail("Choose a living party member.", log);
                    }
                    foreach (var t in targets)
                    {
                        var had = t.Conditions & spell.Conditions;
                        t.Conditions &= ~spell.Conditions;
                        log.Add(new(had == Condition.None ? $"{t.Name} feels refreshed." : $"{t.Name} is cured of {had}.", MessageKind.Good, "heal"));
                    }
                    return new SpellResult(true, log);
                }
            case EffectKind.Raise:
                {
                    if (allyTarget is null || !allyTarget.Has(Condition.Dead))
                    {
                        return Fail("Choose a fallen party member.", log);
                    }
                    allyTarget.Conditions &= ~(Condition.Dead | Condition.Unconscious);
                    allyTarget.Hp = 1;
                    allyTarget.Stats[Stat.Endurance] = Math.Max(3, allyTarget.BaseStat(Stat.Endurance) - 1);
                    log.Add(new($"{allyTarget.Name} draws breath once more!", MessageKind.Good, "levelup"));
                    return new SpellResult(true, log);
                }
            case EffectKind.DebuffArmor:
                {
                    if (combat is null)
                    {
                        return Fail("There is nothing to target here.", log);
                    }
                    foreach (var m in EnemyTargets(spell.Target, combat, enemy))
                    {
                        m.ArmorPenalty += Math.Max(1, spell.Magnitude);
                        log.Add(new($"{m.Label}'s defenses crumble (AC {m.ArmorClass}).", MessageKind.Combat));
                    }
                    return new SpellResult(true, log);
                }
            case EffectKind.BuffArmor:
                if (combat is null)
                {
                    return Fail("That magic only lasts for a battle.", log);
                }
                combat.ArmorBuff += Math.Max(1, spell.Magnitude);
                log.Add(new($"A shimmering ward surrounds the party (AC +{combat.ArmorBuff}).", MessageKind.Good));
                return new SpellResult(true, log);
            case EffectKind.BuffHit:
                if (combat is null)
                {
                    return Fail("That magic only lasts for a battle.", log);
                }
                combat.HitBuff += Math.Max(1, spell.Magnitude);
                log.Add(new($"The party feels blessed (to-hit +{combat.HitBuff}).", MessageKind.Good));
                return new SpellResult(true, log);
            case EffectKind.Light:
                state.AddLight(Math.Max(10, spell.Magnitude), spell.LightRadius);
                log.Add(new("A soft light springs up around the party.", MessageKind.Good));
                return new SpellResult(true, log);
            case EffectKind.Locate:
                {
                    var name = rules.Content.Maps.TryGetValue(state.MapId, out var map) ? map.Def.Name : state.MapId;
                    log.Add(new($"You are in {name} at {state.X},{state.Y} facing {state.Facing}.", MessageKind.Story));
                    return new SpellResult(true, log);
                }
            case EffectKind.CreateFood:
                foreach (var c in party.Where(c => c.IsAlive))
                {
                    c.Food = Math.Min(rules.Content.Config.MaxFood, c.Food + Math.Max(1, spell.Magnitude));
                }
                log.Add(new("Fresh bread and fruit appear in every pack.", MessageKind.Good));
                return new SpellResult(true, log);
            case EffectKind.Recall:
                if (string.IsNullOrEmpty(state.RecallMap) || !rules.Content.Maps.ContainsKey(state.RecallMap))
                {
                    return Fail("The spell has no anchor. Visit an inn first.", log);
                }
                state.MapId = state.RecallMap;
                state.X = state.RecallX;
                state.Y = state.RecallY;
                log.Add(new("The world spins, and the party stands before the inn.", MessageKind.Good, "spell"));
                return new SpellResult(true, log, Teleported: true);
            case EffectKind.RestoreSp:
                {
                    var targets = spell.Target == TargetKind.Party ? party.Where(c => c.IsAlive).ToList() : Single(allyTarget);
                    if (targets.Count == 0)
                    {
                        return Fail("Choose a living party member.", log);
                    }
                    foreach (var t in targets)
                    {
                        t.Sp = Math.Min(t.MaxSp, t.Sp + Amount());
                        log.Add(new($"{t.Name}'s spell points: {t.Sp}/{t.MaxSp}.", MessageKind.Good, "heal"));
                    }
                    return new SpellResult(true, log);
                }
            default:
                return Fail("Nothing happens.", log);
        }
    }

    private static List<Character> Single(Character? c) => c is not null && c.IsAlive ? [c] : [];

    private static SpellResult Fail(string message, List<GameMessage> log)
    {
        log.Clear();
        log.Add(new GameMessage(message));
        return new SpellResult(false, log);
    }

    private static List<MonsterInstance> EnemyTargets(TargetKind kind, CombatEngine combat, MonsterInstance? chosen)
    {
        var active = combat.ActiveMonsters.ToList();
        var primary = chosen is not null && chosen.IsActive ? chosen : active.FirstOrDefault();
        return kind switch
        {
            TargetKind.AllEnemies => active,
            TargetKind.EnemyGroup when primary is not null => active.Where(m => m.Def.Id == primary.Def.Id).ToList(),
            _ when primary is not null => [primary],
            _ => [],
        };
    }
}
