using AVAMMB1.Core.Dice;
using AVAMMB1.Core.Rules;

namespace AVAMMB1.Core.Content;

/// <summary>A playable race (data-driven, see <c>Assets/Data/races.json</c>).</summary>
public sealed class RaceDef
{
    /// <summary>Unique identifier.</summary>
    public string Id { get; init; } = "";
    /// <summary>Display name.</summary>
    public string Name { get; init; } = "";
    /// <summary>Flavor text.</summary>
    public string Description { get; init; } = "";
    /// <summary>Modifiers applied to rolled attributes.</summary>
    public Dictionary<Stat, int> StatModifiers { get; init; } = new();
    /// <summary>Percent resistance per element.</summary>
    public Dictionary<Element, int> Resistances { get; init; } = new();
    /// <summary>Portrait key prefix (combined with sex, e.g. <c>elf_female</c>).</summary>
    public string Portrait { get; init; } = "human";
}

/// <summary>A character class.</summary>
public sealed class ClassDef
{
    /// <summary>Unique identifier.</summary>
    public string Id { get; init; } = "";
    /// <summary>Display name.</summary>
    public string Name { get; init; } = "";
    /// <summary>Flavor text.</summary>
    public string Description { get; init; } = "";
    /// <summary>Hit die size rolled per level.</summary>
    public int HitDie { get; init; } = 8;
    /// <summary>Minimum attribute values required to take this class.</summary>
    public Dictionary<Stat, int> Requirements { get; init; } = new();
    /// <summary>Alignments allowed (empty = any).</summary>
    public List<Alignment> AllowedAlignments { get; init; } = new();
    /// <summary>Spell school, if any.</summary>
    public SpellSchool? SpellSchool { get; init; }
    /// <summary>Character level at which spellcasting begins (1 for full casters).</summary>
    public int SpellStartLevel { get; init; } = 1;
    /// <summary>Melee attack bonus gained per level (fractional).</summary>
    public double AttackPerLevel { get; init; } = 0.5;
    /// <summary>Extra to-hit bonus when shooting missile weapons.</summary>
    public int MissileBonus { get; init; }
    /// <summary>Levels between extra melee attacks per round (0 = never).</summary>
    public int ExtraAttackEvery { get; init; }
    /// <summary>Base experience used to build the level table.</summary>
    public int XpBase { get; init; } = 250;
    /// <summary>Thievery skill percentage at level 1 (0 = none).</summary>
    public int Thievery { get; init; }
    /// <summary>Item ids given to a new character of this class.</summary>
    public List<string> StartingItems { get; init; } = new();
}

/// <summary>An item template.</summary>
public sealed class ItemDef
{
    /// <summary>Unique identifier.</summary>
    public string Id { get; init; } = "";
    /// <summary>Display name.</summary>
    public string Name { get; init; } = "";
    /// <summary>Item category.</summary>
    public ItemKind Kind { get; init; }
    /// <summary>Icon key (file name without extension under Graphics/Items).</summary>
    public string Icon { get; init; } = "";
    /// <summary>Shop price in gold.</summary>
    public int Price { get; init; }
    /// <summary>Weapon damage.</summary>
    public DiceExpression Damage { get; init; }
    /// <summary>Bonus to hit.</summary>
    public int HitBonus { get; init; }
    /// <summary>Bonus to damage.</summary>
    public int DamageBonus { get; init; }
    /// <summary>Armor class bonus.</summary>
    public int ArmorClass { get; init; }
    /// <summary>Two-handed weapons prevent use of a shield.</summary>
    public bool TwoHanded { get; init; }
    /// <summary>Class ids allowed to equip (empty = all).</summary>
    public List<string> Classes { get; init; } = new();
    /// <summary>Stat bonuses while equipped.</summary>
    public Dictionary<Stat, int> StatBonuses { get; init; } = new();
    /// <summary>Spell id cast when the item is used.</summary>
    public string? UseSpell { get; init; }
    /// <summary>Charges (0 = consumed on use for consumables).</summary>
    public int Charges { get; init; }
    /// <summary>Light duration in steps for torches.</summary>
    public int LightSteps { get; init; }
    /// <summary>Food units provided.</summary>
    public int FoodUnits { get; init; }
    /// <summary>Element of weapon damage.</summary>
    public Element Element { get; init; } = Element.Physical;
    /// <summary>Description text.</summary>
    public string Description { get; init; } = "";

    /// <summary>The equipment slot this item occupies, or <c>null</c> when it cannot be equipped.</summary>
    public EquipSlot? Slot => Kind switch
    {
        ItemKind.Weapon => EquipSlot.Weapon,
        ItemKind.Missile => EquipSlot.Missile,
        ItemKind.Armor => EquipSlot.Armor,
        ItemKind.Shield => EquipSlot.Shield,
        ItemKind.Helmet => EquipSlot.Head,
        ItemKind.Gloves => EquipSlot.Hands,
        ItemKind.Boots => EquipSlot.Feet,
        ItemKind.Ring => EquipSlot.Ring,
        ItemKind.Amulet => EquipSlot.Neck,
        _ => null,
    };
}

/// <summary>One attack form of a monster.</summary>
public sealed class MonsterAttackDef
{
    /// <summary>Verb shown in the log ("bites", "slashes").</summary>
    public string Verb { get; init; } = "hits";
    /// <summary>Damage dice.</summary>
    public DiceExpression Damage { get; init; } = new(1, 4, 0);
    /// <summary>Whether this attack can reach the back ranks.</summary>
    public bool Ranged { get; init; }
    /// <summary>Damage element.</summary>
    public Element Element { get; init; } = Element.Physical;
    /// <summary>Condition inflicted on a successful hit.</summary>
    public Condition Inflicts { get; init; }
    /// <summary>Chance (percent) to inflict the condition.</summary>
    public int InflictChance { get; init; }
}

/// <summary>A special ability (spell-like) a monster may use instead of attacking.</summary>
public sealed class MonsterAbilityDef
{
    /// <summary>Name shown in the log.</summary>
    public string Name { get; init; } = "";
    /// <summary>Chance per turn (percent) to use it.</summary>
    public int Chance { get; init; } = 20;
    /// <summary>Damage dealt to each target (may be zero).</summary>
    public DiceExpression Damage { get; init; }
    /// <summary>Whether it hits the whole party (otherwise one random member).</summary>
    public bool AllTargets { get; init; }
    /// <summary>Damage element.</summary>
    public Element Element { get; init; } = Element.Magic;
    /// <summary>Condition inflicted (subject to a saving throw).</summary>
    public Condition Inflicts { get; init; }
    /// <summary>Heals the monster itself by this many dice instead.</summary>
    public DiceExpression SelfHeal { get; init; }
}

/// <summary>An item a monster may drop.</summary>
public sealed class DropDef
{
    /// <summary>Item id.</summary>
    public string Item { get; init; } = "";
    /// <summary>Chance in percent.</summary>
    public int Chance { get; init; } = 10;
}

/// <summary>A monster template.</summary>
public sealed class MonsterDef
{
    /// <summary>Unique identifier.</summary>
    public string Id { get; init; } = "";
    /// <summary>Display name.</summary>
    public string Name { get; init; } = "";
    /// <summary>Plural display name (defaults to Name + "s").</summary>
    public string? Plural { get; init; }
    /// <summary>Sprite key (Graphics/Monsters).</summary>
    public string Sprite { get; init; } = "";
    /// <summary>Monster level (affects to-hit and saves).</summary>
    public int Level { get; init; } = 1;
    /// <summary>Hit point dice.</summary>
    public DiceExpression HitPoints { get; init; } = new(1, 8, 0);
    /// <summary>Armor class.</summary>
    public int ArmorClass { get; init; }
    /// <summary>Speed (initiative).</summary>
    public int Speed { get; init; } = 10;
    /// <summary>Attacks made each turn.</summary>
    public List<MonsterAttackDef> Attacks { get; init; } = new();
    /// <summary>Special abilities.</summary>
    public List<MonsterAbilityDef> Abilities { get; init; } = new();
    /// <summary>Experience awarded.</summary>
    public int Xp { get; init; }
    /// <summary>Gold carried.</summary>
    public DiceExpression Gold { get; init; }
    /// <summary>Possible item drops.</summary>
    public List<DropDef> Drops { get; init; } = new();
    /// <summary>Percent resistance per element.</summary>
    public Dictionary<Element, int> Resistances { get; init; } = new();
    /// <summary>Undead are affected by holy magic and turning.</summary>
    public bool Undead { get; init; }
    /// <summary>Hit points regenerated each round.</summary>
    public int Regenerates { get; init; }
    /// <summary>Monster may flee when badly hurt.</summary>
    public bool Cowardly { get; init; }
    /// <summary>Smart monsters focus the weakest party member.</summary>
    public bool Smart { get; init; }
    /// <summary>Whether the monster can be bribed with gold.</summary>
    public bool Bribable { get; init; }
    /// <summary>Description text.</summary>
    public string Description { get; init; } = "";

    /// <summary>Gets the plural name.</summary>
    public string PluralName => Plural ?? Name + "s";
}

/// <summary>A spell definition.</summary>
public sealed class SpellDef
{
    /// <summary>Unique identifier.</summary>
    public string Id { get; init; } = "";
    /// <summary>Display name.</summary>
    public string Name { get; init; } = "";
    /// <summary>School.</summary>
    public SpellSchool School { get; init; }
    /// <summary>Spell level (1-5).</summary>
    public int Level { get; init; } = 1;
    /// <summary>Spell point cost.</summary>
    public int Cost { get; init; } = 1;
    /// <summary>Effect type.</summary>
    public EffectKind Effect { get; init; }
    /// <summary>Targeting.</summary>
    public TargetKind Target { get; init; }
    /// <summary>Damage or healing amount.</summary>
    public DiceExpression Amount { get; init; }
    /// <summary>Extra amount per caster level (added to dice result).</summary>
    public int PerLevel { get; init; }
    /// <summary>Damage element.</summary>
    public Element Element { get; init; } = Element.Magic;
    /// <summary>Conditions cured or inflicted.</summary>
    public Condition Conditions { get; init; }
    /// <summary>Duration in steps (light) or magnitude for buffs.</summary>
    public int Magnitude { get; init; }
    /// <summary>Whether usable in combat.</summary>
    public bool Combat { get; init; } = true;
    /// <summary>Whether usable while exploring.</summary>
    public bool Explore { get; init; }
    /// <summary>Only affects undead targets.</summary>
    public bool UndeadOnly { get; init; }
    /// <summary>Description text.</summary>
    public string Description { get; init; } = "";
}

/// <summary>Weighted entry in an encounter table.</summary>
public sealed class EncounterEntryDef
{
    /// <summary>Monster id.</summary>
    public string Monster { get; init; } = "";
    /// <summary>Group size dice.</summary>
    public DiceExpression Count { get; init; } = new(1, 3, 0);
    /// <summary>Relative weight.</summary>
    public int Weight { get; init; } = 10;
}

/// <summary>A shop's stock.</summary>
public sealed class ShopDef
{
    /// <summary>Unique identifier.</summary>
    public string Id { get; init; } = "";
    /// <summary>Display name.</summary>
    public string Name { get; init; } = "";
    /// <summary>Greeting text.</summary>
    public string Greeting { get; init; } = "";
    /// <summary>Items for sale.</summary>
    public List<string> Stock { get; init; } = new();
    /// <summary>Price multiplier applied to item prices.</summary>
    public double PriceFactor { get; init; } = 1.0;
}

/// <summary>Global game configuration (start location, starting gold...).</summary>
public sealed class GameConfigDef
{
    /// <summary>Title shown in-game.</summary>
    public string Title { get; init; } = "AVAM&M";
    /// <summary>Introductory text.</summary>
    public string Intro { get; init; } = "";
    /// <summary>Map where new games begin.</summary>
    public string StartMap { get; init; } = "";
    /// <summary>Start X.</summary>
    public int StartX { get; init; }
    /// <summary>Start Y.</summary>
    public int StartY { get; init; }
    /// <summary>Start facing.</summary>
    public Direction StartFacing { get; init; }
    /// <summary>Gold given to each new character.</summary>
    public int StartingGoldPerMember { get; init; } = 100;
    /// <summary>Food given to each new character.</summary>
    public int StartingFood { get; init; } = 10;
    /// <summary>Maximum food carried per character.</summary>
    public int MaxFood { get; init; } = 40;
    /// <summary>Text shown on victory.</summary>
    public string VictoryText { get; init; } = "";
    /// <summary>Premade characters offered by Quick Start.</summary>
    public List<PremadeCharacterDef> Premades { get; init; } = new();
}

/// <summary>A premade character used for a quick start.</summary>
public sealed class PremadeCharacterDef
{
    /// <summary>Name.</summary>
    public string Name { get; init; } = "";
    /// <summary>Race id.</summary>
    public string Race { get; init; } = "";
    /// <summary>Class id.</summary>
    public string Class { get; init; } = "";
    /// <summary>Sex.</summary>
    public Sex Sex { get; init; }
    /// <summary>Alignment.</summary>
    public Alignment Alignment { get; init; }
    /// <summary>Fixed attributes.</summary>
    public Dictionary<Stat, int> Stats { get; init; } = new();
}
