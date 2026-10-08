using AVAMMB1.Core.Characters;
using AVAMMB1.Core.Content;
using AVAMMB1.Core.Dice;
using AVAMMB1.Core.Items;

namespace AVAMMB1.Core.Rules;

/// <summary>Result of a level-up.</summary>
/// <param name="NewLevel">Level reached.</param>
/// <param name="HpGained">Hit points gained.</param>
/// <param name="NewSpellLevel">Newly unlocked spell level, or 0.</param>
public sealed record LevelUpResult(int NewLevel, int HpGained, int NewSpellLevel);

/// <summary>
/// Central game rules: attribute bonuses, armor class, to-hit, damage, experience and spell power.
/// The numbers are original to this project and only inspired by classic 1980s CRPG mechanics.
/// </summary>
/// <param name="db">Content database for class/race/item lookups.</param>
public sealed class Rulebook(ContentDatabase db)
{
    /// <summary>Highest attainable level.</summary>
    public const int MaxLevel = 30;

    /// <summary>The content database.</summary>
    public ContentDatabase Content => db;

    /// <summary>Converts an attribute value into a bonus/penalty.</summary>
    /// <param name="value">Attribute value.</param>
    public static int StatBonus(int value) => value switch
    {
        <= 3 => -3,
        <= 5 => -2,
        <= 8 => -1,
        <= 12 => 0,
        <= 15 => 1,
        <= 18 => 2,
        <= 21 => 3,
        _ => 4,
    };

    /// <summary>Attribute value including equipment bonuses.</summary>
    /// <param name="c">Character.</param>
    /// <param name="stat">Attribute.</param>
    public int Stat(Character c, Stat stat)
    {
        var v = c.BaseStat(stat);
        foreach (var item in c.Equipment.Values)
        {
            if (db.Items.TryGetValue(item.ItemId, out var def) && def.StatBonuses.TryGetValue(stat, out var b))
            {
                v += b;
            }
        }
        return Math.Clamp(v, 1, 50);
    }

    /// <summary>Bonus derived from an effective attribute.</summary>
    /// <param name="c">Character.</param>
    /// <param name="stat">Attribute.</param>
    public int Bonus(Character c, Stat stat) => StatBonus(Stat(c, stat));

    private ItemDef? Equipped(Character c, EquipSlot slot) =>
        c.Equipment.TryGetValue(slot, out var inst) && db.Items.TryGetValue(inst.ItemId, out var def) ? def : null;

    /// <summary>Armor class (higher is better).</summary>
    /// <param name="c">Character.</param>
    public int ArmorClass(Character c)
    {
        var ac = Math.Max(0, Bonus(c, Rules.Stat.Speed));
        foreach (var item in c.Equipment.Values)
        {
            if (db.Items.TryGetValue(item.ItemId, out var def))
            {
                ac += def.ArmorClass;
            }
        }
        return Math.Max(0, ac);
    }

    /// <summary>Melee to-hit bonus.</summary>
    /// <param name="c">Character.</param>
    public int MeleeAttackBonus(Character c)
    {
        var cls = db.Class(c.Class);
        var bonus = (int)Math.Floor(c.Level * cls.AttackPerLevel) + Bonus(c, Rules.Stat.Accuracy) + (Equipped(c, EquipSlot.Weapon)?.HitBonus ?? 0);
        return c.Has(Condition.Blinded) ? bonus - 4 : bonus;
    }

    /// <summary>Missile to-hit bonus.</summary>
    /// <param name="c">Character.</param>
    public int MissileAttackBonus(Character c)
    {
        var cls = db.Class(c.Class);
        var bonus = (int)Math.Floor(c.Level * cls.AttackPerLevel) + cls.MissileBonus + Bonus(c, Rules.Stat.Accuracy) + (Equipped(c, EquipSlot.Missile)?.HitBonus ?? 0);
        return c.Has(Condition.Blinded) ? bonus - 4 : bonus;
    }

    /// <summary>Whether the character has a missile weapon equipped.</summary>
    /// <param name="c">Character.</param>
    public bool HasMissileWeapon(Character c) => Equipped(c, EquipSlot.Missile) is not null;

    /// <summary>Rolls melee damage for one hit.</summary>
    /// <param name="c">Attacker.</param>
    /// <param name="rng">Random source.</param>
    public int RollMeleeDamage(Character c, IRandomSource rng)
    {
        var w = Equipped(c, EquipSlot.Weapon);
        var dice = w is null ? new DiceExpression(1, 2, 0) : w.Damage;
        return Math.Max(1, dice.Roll(rng) + Bonus(c, Rules.Stat.Might) + (w?.DamageBonus ?? 0));
    }

    /// <summary>Rolls missile damage for one hit.</summary>
    /// <param name="c">Attacker.</param>
    /// <param name="rng">Random source.</param>
    public int RollMissileDamage(Character c, IRandomSource rng)
    {
        var w = Equipped(c, EquipSlot.Missile);
        var dice = w?.Damage ?? new DiceExpression(1, 2, 0);
        return Math.Max(1, dice.Roll(rng) + (w?.DamageBonus ?? 0));
    }

    /// <summary>Melee attacks per round.</summary>
    /// <param name="c">Character.</param>
    public int AttacksPerRound(Character c)
    {
        var cls = db.Class(c.Class);
        return 1 + (cls.ExtraAttackEvery > 0 ? c.Level / cls.ExtraAttackEvery : 0);
    }

    /// <summary>
    /// Resolves a to-hit roll: a natural 20 always hits, a natural 1 always misses,
    /// otherwise <c>d20 + bonus &gt;= 8 + targetAC</c>.
    /// </summary>
    /// <param name="roll">The d20 result.</param>
    /// <param name="attackBonus">Attacker's bonus.</param>
    /// <param name="targetArmorClass">Defender's armor class.</param>
    public static bool IsHit(int roll, int attackBonus, int targetArmorClass) =>
        roll == 20 || (roll != 1 && roll + attackBonus >= 8 + targetArmorClass);

    /// <summary>Total experience needed to reach a level.</summary>
    /// <param name="cls">Class definition.</param>
    /// <param name="level">Target level.</param>
    public static long XpForLevel(ClassDef cls, int level)
    {
        if (level <= 1)
        {
            return 0;
        }
        var doubling = Math.Min(level, 12);
        var xp = cls.XpBase * ((1L << (doubling - 1)) - 1);
        if (level > 12)
        {
            xp += (level - 12) * cls.XpBase * 1024L;
        }
        return xp;
    }

    /// <summary>Experience required for the character's next level.</summary>
    /// <param name="c">Character.</param>
    public long XpForNextLevel(Character c) => XpForLevel(db.Class(c.Class), c.Level + 1);

    /// <summary>Whether the character has enough experience to train.</summary>
    /// <param name="c">Character.</param>
    public bool CanLevelUp(Character c) => c.Level < MaxLevel && c.IsAlive && c.Experience >= XpForNextLevel(c);

    /// <summary>Gold cost to train from the character's current level.</summary>
    /// <param name="c">Character.</param>
    public static int TrainingCost(Character c) => 50 * c.Level * c.Level;

    /// <summary>Casting level (0 for non casters or before the class's spell start level).</summary>
    /// <param name="c">Character.</param>
    public int CasterLevel(Character c)
    {
        var cls = db.Class(c.Class);
        return cls.SpellSchool is null || c.Level < cls.SpellStartLevel ? 0 : c.Level - cls.SpellStartLevel + 1;
    }

    /// <summary>Highest spell level the character may cast (0-5).</summary>
    /// <param name="c">Character.</param>
    public int MaxSpellLevel(Character c) => Math.Min(5, (CasterLevel(c) + 1) / 2);

    /// <summary>The attribute that powers the character's magic.</summary>
    /// <param name="c">Character.</param>
    public Stat CastingStat(Character c) =>
        db.Class(c.Class).SpellSchool == SpellSchool.Cleric ? Rules.Stat.Personality : Rules.Stat.Intellect;

    /// <summary>Maximum spell points.</summary>
    /// <param name="c">Character.</param>
    public int ComputeMaxSp(Character c)
    {
        var lvl = CasterLevel(c);
        return lvl == 0 ? 0 : lvl * (2 + Math.Max(0, Bonus(c, CastingStat(c)))) + 1;
    }

    /// <summary>Hit points at level one.</summary>
    /// <param name="cls">Class.</param>
    /// <param name="endurance">Endurance value.</param>
    public static int StartingHp(ClassDef cls, int endurance) => Math.Max(1, cls.HitDie + StatBonus(endurance));

    /// <summary>Spells the character can currently cast.</summary>
    /// <param name="c">Character.</param>
    public IEnumerable<SpellDef> KnownSpells(Character c)
    {
        var school = db.Class(c.Class).SpellSchool;
        var max = MaxSpellLevel(c);
        return school is null
            ? []
            : db.Spells.Values.Where(s => s.Learnable && s.School == school && s.Level <= max).OrderBy(s => s.Level).ThenBy(s => s.Name, StringComparer.Ordinal);
    }

    /// <summary>Thievery skill percentage (trap disarming, lock picking).</summary>
    /// <param name="c">Character.</param>
    public int Thievery(Character c)
    {
        var cls = db.Class(c.Class);
        return cls.Thievery == 0 ? 0 : Math.Clamp(cls.Thievery + c.Level * 4 + Bonus(c, Rules.Stat.Speed) * 5, 5, 95);
    }

    /// <summary>Saving throw against a hostile effect of the given level.</summary>
    /// <param name="c">Defender.</param>
    /// <param name="effectLevel">Level of the attacker/effect.</param>
    /// <param name="rng">Random source.</param>
    public bool SavingThrow(Character c, int effectLevel, IRandomSource rng) =>
        rng.Die(20) + c.Level / 2 + Bonus(c, Rules.Stat.Luck) >= 10 + effectLevel / 2;

    /// <summary>Percent resistance of a character to an element (race based).</summary>
    /// <param name="c">Character.</param>
    /// <param name="element">Element.</param>
    public int Resistance(Character c, Element element) =>
        db.Races.TryGetValue(c.Race, out var r) && r.Resistances.TryGetValue(element, out var v) ? v : 0;

    /// <summary>Whether the character's class may use an item.</summary>
    /// <param name="c">Character.</param>
    /// <param name="item">Item.</param>
    public static bool CanUse(Character c, ItemDef item) => item.Classes.Count == 0 || item.Classes.Contains(c.Class);

    /// <summary>Raises the character one level if eligible.</summary>
    /// <param name="c">Character.</param>
    /// <param name="rng">Random source for the hit point roll.</param>
    /// <returns>The result, or <c>null</c> if not eligible.</returns>
    public LevelUpResult? LevelUp(Character c, IRandomSource rng)
    {
        if (!CanLevelUp(c))
        {
            return null;
        }
        var cls = db.Class(c.Class);
        var oldSpellLevel = MaxSpellLevel(c);
        c.Level++;
        var gain = Math.Max(1, rng.Die(cls.HitDie) + Bonus(c, Rules.Stat.Endurance));
        c.MaxHp += gain;
        c.Hp += gain;
        c.MaxSp = ComputeMaxSp(c);
        c.Sp = c.MaxSp;
        var newSpellLevel = MaxSpellLevel(c);
        return new LevelUpResult(c.Level, gain, newSpellLevel > oldSpellLevel ? newSpellLevel : 0);
    }

    /// <summary>Applies damage to a character, handling unconsciousness and death.</summary>
    /// <param name="c">Character.</param>
    /// <param name="amount">Damage.</param>
    /// <returns>True if the character died or fell unconscious as a result.</returns>
    public bool ApplyDamage(Character c, int amount)
    {
        if (!c.IsAlive || amount <= 0)
        {
            return false;
        }
        c.Hp -= amount;
        c.Conditions &= ~Condition.Asleep;
        if (c.Hp <= -Stat(c, Rules.Stat.Endurance))
        {
            c.Conditions = (c.Conditions | Condition.Dead) & ~Condition.Unconscious;
            return true;
        }
        if (c.Hp <= 0 && !c.Has(Condition.Unconscious))
        {
            c.Conditions |= Condition.Unconscious;
            return true;
        }
        return false;
    }

    /// <summary>Heals a living character, reviving them from unconsciousness if HP rises above zero.</summary>
    /// <param name="c">Character.</param>
    /// <param name="amount">Hit points restored.</param>
    /// <returns>Actual amount healed.</returns>
    public static int Heal(Character c, int amount)
    {
        if (!c.IsAlive || amount <= 0)
        {
            return 0;
        }
        var before = c.Hp;
        c.Hp = Math.Min(c.MaxHp, c.Hp + amount);
        if (c.Hp > 0)
        {
            c.Conditions &= ~Condition.Unconscious;
        }
        return c.Hp - before;
    }

    /// <summary>Sell price for an item.</summary>
    /// <param name="item">Item.</param>
    public static int SellPrice(ItemDef item) => Math.Max(0, item.Price / 2);

    /// <summary>Gets the definition of an item instance.</summary>
    /// <param name="item">Instance.</param>
    public ItemDef Def(ItemInstance item) => db.Item(item.ItemId);
}
