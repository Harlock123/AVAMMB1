using AVAMMB1.Core.Dice;
using AVAMMB1.Core.Rules;

namespace AVAMMB1.Core.Content;

/// <summary>A playable race (data-driven, see <c>Assets/Data/races.json</c>).</summary>
public sealed class RaceDef
{
    /// <summary>Unique identifier.</summary>
    public string Id { get; set; } = "";
    /// <summary>Display name.</summary>
    public string Name { get; set; } = "";
    /// <summary>Flavor text.</summary>
    public string Description { get; set; } = "";
    /// <summary>Modifiers applied to rolled attributes.</summary>
    public Dictionary<Stat, int> StatModifiers { get; set; } = new();
    /// <summary>Percent resistance per element.</summary>
    public Dictionary<Element, int> Resistances { get; set; } = new();
    /// <summary>Portrait key prefix (combined with sex, e.g. <c>elf_female</c>).</summary>
    public string Portrait { get; set; } = "human";
}

/// <summary>A character class.</summary>
public sealed class ClassDef
{
    /// <summary>Unique identifier.</summary>
    public string Id { get; set; } = "";
    /// <summary>Special abilities (see <see cref="ClassAbility"/>).</summary>
    public List<string> Abilities { get; set; } = new();
    /// <summary>Display name.</summary>
    public string Name { get; set; } = "";
    /// <summary>Flavor text.</summary>
    public string Description { get; set; } = "";
    /// <summary>Hit die size rolled per level.</summary>
    public int HitDie { get; set; } = 8;
    /// <summary>Minimum attribute values required to take this class.</summary>
    public Dictionary<Stat, int> Requirements { get; set; } = new();
    /// <summary>Alignments allowed (empty = any).</summary>
    public List<Alignment> AllowedAlignments { get; set; } = new();
    /// <summary>Spell school, if any.</summary>
    public SpellSchool? SpellSchool { get; set; }
    /// <summary>Character level at which spellcasting begins (1 for full casters).</summary>
    public int SpellStartLevel { get; set; } = 1;
    /// <summary>Melee attack bonus gained per level (fractional).</summary>
    public double AttackPerLevel { get; set; } = 0.5;
    /// <summary>Extra to-hit bonus when shooting missile weapons.</summary>
    public int MissileBonus { get; set; }
    /// <summary>Levels between extra melee attacks per round (0 = never).</summary>
    public int ExtraAttackEvery { get; set; }
    /// <summary>Base experience used to build the level table.</summary>
    public int XpBase { get; set; } = 250;
    /// <summary>Thievery skill percentage at level 1 (0 = none).</summary>
    public int Thievery { get; set; }
    /// <summary>Item ids given to a new character of this class.</summary>
    public List<string> StartingItems { get; set; } = new();
    /// <summary>Paper-doll image layers drawn over the race portrait (<c>{sex}</c> is replaced by male/female).</summary>
    public List<string> PortraitLayers { get; set; } = new();
}

/// <summary>An item template.</summary>
public sealed class ItemDef
{
    /// <summary>Unique identifier.</summary>
    public string Id { get; set; } = "";
    /// <summary>Display name.</summary>
    public string Name { get; set; } = "";
    /// <summary>Item category.</summary>
    public ItemKind Kind { get; set; }
    /// <summary>Icon key (file name without extension under Graphics/Items).</summary>
    public string Icon { get; set; } = "";
    /// <summary>Shop price in gold.</summary>
    public int Price { get; set; }
    /// <summary>Weapon damage.</summary>
    public DiceExpression Damage { get; set; }
    /// <summary>Bonus to hit.</summary>
    public int HitBonus { get; set; }
    /// <summary>Bonus to damage.</summary>
    public int DamageBonus { get; set; }
    /// <summary>Armor class bonus.</summary>
    public int ArmorClass { get; set; }
    /// <summary>Two-handed weapons prevent use of a shield.</summary>
    public bool TwoHanded { get; set; }
    /// <summary>Class ids allowed to equip (empty = all).</summary>
    public List<string> Classes { get; set; } = new();
    /// <summary>Stat bonuses while equipped.</summary>
    public Dictionary<Stat, int> StatBonuses { get; set; } = new();
    /// <summary>Spell taught by reading this item (tomes).</summary>
    public string? TeachSpell { get; set; }
    /// <summary>Spell id cast when the item is used.</summary>
    public string? UseSpell { get; set; }
    /// <summary>Charges (0 = consumed on use for consumables).</summary>
    public int Charges { get; set; }
    /// <summary>Light duration in steps for torches.</summary>
    public int LightSteps { get; set; }
    /// <summary>How far the light reaches in dark places, in squares (torches and lanterns).</summary>
    public int LightRadius { get; set; }
    /// <summary>Lanterns: steps of oil a full lantern holds (0 = never needs oil). Stored in the item's charges.</summary>
    public int FuelCapacity { get; set; }
    /// <summary>Oil: steps of light one flask adds to a lantern.</summary>
    public int FuelAmount { get; set; }
    /// <summary>Food units provided.</summary>
    public int FoodUnits { get; set; }
    /// <summary>Element of weapon damage.</summary>
    public Element Element { get; set; } = Element.Physical;
    /// <summary>Description text.</summary>
    public string Description { get; set; } = "";

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
        ItemKind.Lantern => EquipSlot.Light,
        _ => null,
    };
}

/// <summary>One attack form of a monster.</summary>
public sealed class MonsterAttackDef
{
    /// <summary>Verb shown in the log ("bites", "slashes").</summary>
    public string Verb { get; set; } = "hits";
    /// <summary>Damage dice.</summary>
    public DiceExpression Damage { get; set; } = new(1, 4, 0);
    /// <summary>Whether this attack can reach the back ranks.</summary>
    public bool Ranged { get; set; }
    /// <summary>Damage element.</summary>
    public Element Element { get; set; } = Element.Physical;
    /// <summary>Condition inflicted on a successful hit.</summary>
    public Condition Inflicts { get; set; }
    /// <summary>Chance (percent) to inflict the condition.</summary>
    public int InflictChance { get; set; }
}

/// <summary>A special ability (spell-like) a monster may use instead of attacking.</summary>
public sealed class MonsterAbilityDef
{
    /// <summary>Name shown in the log.</summary>
    public string Name { get; set; } = "";
    /// <summary>Chance per turn (percent) to use it.</summary>
    public int Chance { get; set; } = 20;
    /// <summary>Damage dealt to each target (may be zero).</summary>
    public DiceExpression Damage { get; set; }
    /// <summary>Whether it hits the whole party (otherwise one random member).</summary>
    public bool AllTargets { get; set; }
    /// <summary>Damage element.</summary>
    public Element Element { get; set; } = Element.Magic;
    /// <summary>Condition inflicted (subject to a saving throw).</summary>
    public Condition Inflicts { get; set; }
    /// <summary>Heals the monster itself by this many dice instead.</summary>
    public DiceExpression SelfHeal { get; set; }
}

/// <summary>An item a monster may drop.</summary>
public sealed class DropDef
{
    /// <summary>Item id.</summary>
    public string Item { get; set; } = "";
    /// <summary>Chance in percent.</summary>
    public int Chance { get; set; } = 10;
}

/// <summary>A monster template.</summary>
public sealed class MonsterDef
{
    /// <summary>Unique identifier.</summary>
    public string Id { get; set; } = "";
    /// <summary>Display name.</summary>
    public string Name { get; set; } = "";
    /// <summary>Plural display name (defaults to Name + "s").</summary>
    public string? Plural { get; set; }
    /// <summary>Sprite key (Graphics/Monsters).</summary>
    public string Sprite { get; set; } = "";
    /// <summary>A unique boss: battles including it play the boss theme.</summary>
    public bool Boss { get; set; }
    /// <summary>Monster level (affects to-hit and saves).</summary>
    public int Level { get; set; } = 1;
    /// <summary>Hit point dice.</summary>
    public DiceExpression HitPoints { get; set; } = new(1, 8, 0);
    /// <summary>Armor class.</summary>
    public int ArmorClass { get; set; }
    /// <summary>Speed (initiative).</summary>
    public int Speed { get; set; } = 10;
    /// <summary>Attacks made each turn.</summary>
    public List<MonsterAttackDef> Attacks { get; set; } = new();
    /// <summary>Special abilities.</summary>
    public List<MonsterAbilityDef> Abilities { get; set; } = new();
    /// <summary>Experience awarded.</summary>
    public int Xp { get; set; }
    /// <summary>Gold carried.</summary>
    public DiceExpression Gold { get; set; }
    /// <summary>Possible item drops.</summary>
    public List<DropDef> Drops { get; set; } = new();
    /// <summary>Percent resistance per element.</summary>
    public Dictionary<Element, int> Resistances { get; set; } = new();
    /// <summary>Undead are affected by holy magic and turning.</summary>
    public bool Undead { get; set; }
    /// <summary>Hit points regenerated each round.</summary>
    public int Regenerates { get; set; }
    /// <summary>Monster may flee when badly hurt.</summary>
    public bool Cowardly { get; set; }
    /// <summary>Smart monsters focus the weakest party member.</summary>
    public bool Smart { get; set; }
    /// <summary>Whether the monster can be bribed with gold.</summary>
    public bool Bribable { get; set; }
    /// <summary>Description text.</summary>
    public string Description { get; set; } = "";

    /// <summary>Gets the plural name.</summary>
    public string PluralName => Plural ?? Name + "s";
    /// <summary>The name with "a" or "an" (none for names like "The Sun King").</summary>
    public string NameWithArticle =>
        Name.StartsWith("The ", StringComparison.Ordinal) ? Name
        : "AEIOUaeiou".Contains(Name[..1], StringComparison.Ordinal) ? "an " + Name
        : "a " + Name;
}

/// <summary>A spell definition.</summary>
public sealed class SpellDef
{
    /// <summary>Unique identifier.</summary>
    public string Id { get; set; } = "";
    /// <summary>Display name.</summary>
    public string Name { get; set; } = "";
    /// <summary>Light spells: how far the light reaches, in squares.</summary>
    public int LightRadius { get; set; } = 6;
    /// <summary>School.</summary>
    public SpellSchool School { get; set; }
    /// <summary>Spell level (1-5).</summary>
    public int Level { get; set; } = 1;
    /// <summary>Spell point cost.</summary>
    public int Cost { get; set; } = 1;
    /// <summary>Effect type.</summary>
    public EffectKind Effect { get; set; }
    /// <summary>Targeting.</summary>
    public TargetKind Target { get; set; }
    /// <summary>Damage or healing amount.</summary>
    public DiceExpression Amount { get; set; }
    /// <summary>Extra amount per caster level (added to dice result).</summary>
    public int PerLevel { get; set; }
    /// <summary>Damage element.</summary>
    public Element Element { get; set; } = Element.Magic;
    /// <summary>Conditions cured or inflicted.</summary>
    public Condition Conditions { get; set; }
    /// <summary>Duration in steps (light) or magnitude for buffs.</summary>
    public int Magnitude { get; set; }
    /// <summary>Whether usable in combat.</summary>
    public bool Combat { get; set; } = true;
    /// <summary>Whether usable while exploring.</summary>
    public bool Explore { get; set; }
    /// <summary>Only affects undead targets.</summary>
    public bool UndeadOnly { get; set; }
    /// <summary>Whether characters can learn it (false for item-only effects).</summary>
    public bool Learnable { get; set; } = true;
    /// <summary>Learned only by reading a tome, not automatically on gaining levels.</summary>
    public bool Tome { get; set; }
    /// <summary>Description text.</summary>
    public string Description { get; set; } = "";
}

/// <summary>Weighted entry in an encounter table.</summary>
public sealed class EncounterEntryDef
{
    /// <summary>Monster id.</summary>
    public string Monster { get; set; } = "";
    /// <summary>Group size dice.</summary>
    public DiceExpression Count { get; set; } = new(1, 3, 0);
    /// <summary>Relative weight.</summary>
    public int Weight { get; set; } = 10;
}

/// <summary>A shop's stock.</summary>
public sealed class ShopDef
{
    /// <summary>Unique identifier.</summary>
    public string Id { get; set; } = "";
    /// <summary>Display name.</summary>
    public string Name { get; set; } = "";
    /// <summary>Greeting text.</summary>
    public string Greeting { get; set; } = "";
    /// <summary>Items for sale.</summary>
    public List<string> Stock { get; set; } = new();
    /// <summary>Price multiplier applied to item prices.</summary>
    public double PriceFactor { get; set; } = 1.0;
}

/// <summary>Global game configuration (start location, starting gold...).</summary>
public sealed class GameConfigDef
{
    /// <summary>Title shown in-game.</summary>
    public string Title { get; set; } = "AVAM&M";
    /// <summary>Introductory text.</summary>
    public string Intro { get; set; } = "";
    /// <summary>Map where new games begin.</summary>
    public string StartMap { get; set; } = "";
    /// <summary>Start X.</summary>
    public int StartX { get; set; }
    /// <summary>Start Y.</summary>
    public int StartY { get; set; }
    /// <summary>Start facing.</summary>
    public Direction StartFacing { get; set; }
    /// <summary>Gold given to each new character.</summary>
    public int StartingGoldPerMember { get; set; } = 100;
    /// <summary>Food given to each new character.</summary>
    public int StartingFood { get; set; } = 10;
    /// <summary>Maximum food carried per character.</summary>
    public int MaxFood { get; set; } = 40;
    /// <summary>Text shown on victory.</summary>
    public string VictoryText { get; set; } = "";
    /// <summary>Premade characters offered by Quick Start.</summary>
    public List<PremadeCharacterDef> Premades { get; set; } = new();
}

/// <summary>A premade character used for a quick start.</summary>
public sealed class PremadeCharacterDef
{
    /// <summary>Name.</summary>
    public string Name { get; set; } = "";
    /// <summary>Race id.</summary>
    public string Race { get; set; } = "";
    /// <summary>Class id.</summary>
    public string Class { get; set; } = "";
    /// <summary>Sex.</summary>
    public Sex Sex { get; set; }
    /// <summary>Alignment.</summary>
    public Alignment Alignment { get; set; }
    /// <summary>Fixed attributes.</summary>
    public Dictionary<Stat, int> Stats { get; set; } = new();
}

/// <summary>A quest shown in the journal. Stages are reached by story flags, items held or maps visited.</summary>
public sealed class QuestDef
{
    /// <summary>Unique identifier.</summary>
    public string Id { get; set; } = "";
    /// <summary>Journal title.</summary>
    public string Title { get; set; } = "";
    /// <summary>Whether this is the main quest (listed first).</summary>
    public bool Main { get; set; }
    /// <summary>Stages in story order; the quest appears once any stage is reached.</summary>
    public List<QuestStageDef> Stages { get; set; } = new();
    /// <summary>Flag that marks the quest complete.</summary>
    public string? DoneFlag { get; set; }
    /// <summary>Journal text once complete.</summary>
    public string DoneText { get; set; } = "";
}

/// <summary>One journal entry of a quest. Set exactly one of <see cref="Flag"/>, <see cref="Item"/> or <see cref="Visited"/>.</summary>
public sealed class QuestStageDef
{
    /// <summary>Reached when this story flag is set.</summary>
    public string? Flag { get; set; }
    /// <summary>Reached while a party member carries this item.</summary>
    public string? Item { get; set; }
    /// <summary>Reached once the party has set foot on this map.</summary>
    public string? Visited { get; set; }
    /// <summary>Where to go next from this stage, when the game cannot work it out from the map events.</summary>
    public QuestGoalDef? Goal { get; set; }
    /// <summary>Journal text.</summary>
    public string Text { get; set; } = "";
}

/// <summary>An explicit "go here next" for a quest stage.</summary>
public sealed class QuestGoalDef
{
    /// <summary>Map id.</summary>
    public string Map { get; set; } = "";
    /// <summary>Cell X.</summary>
    public int X { get; set; }
    /// <summary>Cell Y.</summary>
    public int Y { get; set; }
    /// <summary>What is there (shown in the journal), e.g. "the kobold warren".</summary>
    public string? Name { get; set; }
}

/// <summary>Ability names used in <see cref="ClassDef.Abilities"/>.</summary>
public static class ClassAbility
{
    /// <summary>Battle: protect an ally this round - attacks aimed at them strike the guard instead.</summary>
    public const string Guard = "guard";
    /// <summary>Battle, once per fight: heal an ally (3 x level + 5) and cure poison.</summary>
    public const string LayOnHands = "layOnHands";
    /// <summary>Battle: one careful missile shot at +4 to hit for double damage.</summary>
    public const string AimedShot = "aimedShot";
    /// <summary>Double damage on the first round of a battle.</summary>
    public const string SneakAttack = "sneakAttack";
    /// <summary>Exploring: try to pick locked doors.</summary>
    public const string PickLocks = "pickLocks";

    /// <summary>All known abilities.</summary>
    public static readonly IReadOnlyList<string> All = [Guard, LayOnHands, AimedShot, SneakAttack, PickLocks];
}
