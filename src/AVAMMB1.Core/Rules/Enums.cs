namespace AVAMMB1.Core.Rules;

/// <summary>The seven primary character attributes.</summary>
public enum Stat
{
    /// <summary>Physical strength; adds to melee damage.</summary>
    Might,
    /// <summary>Reasoning; powers sorcery.</summary>
    Intellect,
    /// <summary>Force of will and faith; powers clerical magic.</summary>
    Personality,
    /// <summary>Toughness; adds hit points.</summary>
    Endurance,
    /// <summary>Quickness; affects initiative and armor class.</summary>
    Speed,
    /// <summary>Precision; adds to hit chance.</summary>
    Accuracy,
    /// <summary>Fortune; affects saving throws and traps.</summary>
    Luck,
}

/// <summary>Moral alignment.</summary>
public enum Alignment
{
    /// <summary>Good.</summary>
    Good,
    /// <summary>Neutral.</summary>
    Neutral,
    /// <summary>Evil.</summary>
    Evil,
}

/// <summary>Character sex (affects portrait only).</summary>
public enum Sex
{
    /// <summary>Male.</summary>
    Male,
    /// <summary>Female.</summary>
    Female,
}

/// <summary>Status conditions that can afflict characters and monsters.</summary>
[Flags]
public enum Condition
{
    /// <summary>No conditions.</summary>
    None = 0,
    /// <summary>Asleep; cannot act until woken by damage or rest.</summary>
    Asleep = 1 << 0,
    /// <summary>Blinded; heavy penalty to hit.</summary>
    Blinded = 1 << 1,
    /// <summary>Silenced; cannot cast spells.</summary>
    Silenced = 1 << 2,
    /// <summary>Poisoned; loses hit points over time and cannot recover naturally.</summary>
    Poisoned = 1 << 3,
    /// <summary>Diseased; recovers only half hit points when resting.</summary>
    Diseased = 1 << 4,
    /// <summary>Paralyzed; cannot act.</summary>
    Paralyzed = 1 << 5,
    /// <summary>Unconscious from wounds (HP at or below zero).</summary>
    Unconscious = 1 << 6,
    /// <summary>Dead; requires resurrection.</summary>
    Dead = 1 << 7,
    /// <summary>Turned to stone; requires a temple.</summary>
    Stoned = 1 << 8,
}

/// <summary>Equipment slots on a character.</summary>
public enum EquipSlot
{
    /// <summary>Melee weapon.</summary>
    Weapon,
    /// <summary>Missile weapon (bow, sling, crossbow).</summary>
    Missile,
    /// <summary>Body armor.</summary>
    Armor,
    /// <summary>Shield.</summary>
    Shield,
    /// <summary>Helmet.</summary>
    Head,
    /// <summary>Gloves or gauntlets.</summary>
    Hands,
    /// <summary>Boots.</summary>
    Feet,
    /// <summary>A ring.</summary>
    Ring,
    /// <summary>An amulet.</summary>
    Neck,
    /// <summary>A light source (lantern) that lights the way for the whole party.</summary>
    Light,
}

/// <summary>Broad item categories.</summary>
public enum ItemKind
{
    /// <summary>Melee weapon.</summary>
    Weapon,
    /// <summary>Missile weapon.</summary>
    Missile,
    /// <summary>Body armor.</summary>
    Armor,
    /// <summary>Shield.</summary>
    Shield,
    /// <summary>Helmet.</summary>
    Helmet,
    /// <summary>Gloves.</summary>
    Gloves,
    /// <summary>Boots.</summary>
    Boots,
    /// <summary>Ring.</summary>
    Ring,
    /// <summary>Amulet.</summary>
    Amulet,
    /// <summary>Drinkable potion (single use).</summary>
    Potion,
    /// <summary>Readable scroll (single use).</summary>
    Scroll,
    /// <summary>Wand or other charged item.</summary>
    Wand,
    /// <summary>Food rations.</summary>
    Food,
    /// <summary>Light source.</summary>
    Torch,
    /// <summary>Quest object.</summary>
    Quest,
    /// <summary>A spell tome: reading it permanently teaches a spell.</summary>
    Tome,
    /// <summary>Anything else.</summary>
    Misc,
    /// <summary>An equippable lantern: a bright light that burns oil (see <see cref="Content.ItemDef.FuelCapacity"/>).</summary>
    Lantern,
    /// <summary>A flask of oil: refills a lantern, or is thrown as a fire bomb in battle.</summary>
    Oil,
}

/// <summary>Damage elements used for resistances.</summary>
public enum Element
{
    /// <summary>Plain physical damage.</summary>
    Physical,
    /// <summary>Fire.</summary>
    Fire,
    /// <summary>Cold.</summary>
    Cold,
    /// <summary>Lightning.</summary>
    Electric,
    /// <summary>Acid.</summary>
    Acid,
    /// <summary>Raw magic.</summary>
    Magic,
    /// <summary>Holy energy (extra effective against undead).</summary>
    Holy,
    /// <summary>Poison.</summary>
    Poison,
}

/// <summary>The schools of magic.</summary>
public enum SpellSchool
{
    /// <summary>Clerical (Personality based) magic.</summary>
    Cleric,
    /// <summary>Sorcerous (Intellect based) magic.</summary>
    Sorcerer,
}

/// <summary>What a spell or ability targets.</summary>
public enum TargetKind
{
    /// <summary>No target (utility effect).</summary>
    None,
    /// <summary>One party member.</summary>
    Ally,
    /// <summary>The whole party.</summary>
    Party,
    /// <summary>One enemy.</summary>
    Enemy,
    /// <summary>All enemies of the same kind as the chosen enemy.</summary>
    EnemyGroup,
    /// <summary>Every enemy.</summary>
    AllEnemies,
}

/// <summary>Spell / item effect types.</summary>
public enum EffectKind
{
    /// <summary>Deals damage.</summary>
    Damage,
    /// <summary>Restores hit points.</summary>
    Heal,
    /// <summary>Removes conditions.</summary>
    Cure,
    /// <summary>Returns the dead to life.</summary>
    Raise,
    /// <summary>Inflicts a condition on enemies.</summary>
    Inflict,
    /// <summary>Temporary armor class bonus for the party (combat).</summary>
    BuffArmor,
    /// <summary>Temporary to-hit bonus for the party (combat).</summary>
    BuffHit,
    /// <summary>Provides light in dark places.</summary>
    Light,
    /// <summary>Reveals current location.</summary>
    Locate,
    /// <summary>Creates food for each party member.</summary>
    CreateFood,
    /// <summary>Returns the party to the last visited town.</summary>
    Recall,
    /// <summary>Restores spell points.</summary>
    RestoreSp,
    /// <summary>Lowers enemy armor class (by <c>magnitude</c>) for the rest of the battle.</summary>
    DebuffArmor,
    /// <summary>Uncovers secret doors within <c>magnitude</c> squares.</summary>
    RevealSecrets,
    /// <summary>The party floats over floor traps and pits for <c>magnitude</c> steps (plus 10 a caster level).</summary>
    Levitate,
    /// <summary>Senses the guardians and lairs still waiting on this level: what and which way.</summary>
    SenseMinds,
}

/// <summary>Cardinal facing on the grid.</summary>
public enum Direction
{
    /// <summary>North (towards y-1).</summary>
    North,
    /// <summary>East (towards x+1).</summary>
    East,
    /// <summary>South (towards y+1).</summary>
    South,
    /// <summary>West (towards x-1).</summary>
    West,
}

/// <summary>Helpers for <see cref="Direction"/>.</summary>
public static class DirectionExtensions
{
    /// <summary>Turns 90 degrees clockwise.</summary>
    /// <param name="d">Current facing.</param>
    public static Direction Right(this Direction d) => (Direction)(((int)d + 1) % 4);

    /// <summary>Turns 90 degrees counter-clockwise.</summary>
    /// <param name="d">Current facing.</param>
    public static Direction Left(this Direction d) => (Direction)(((int)d + 3) % 4);

    /// <summary>Turns around.</summary>
    /// <param name="d">Current facing.</param>
    public static Direction Opposite(this Direction d) => (Direction)(((int)d + 2) % 4);

    /// <summary>X component of a unit step in this direction.</summary>
    /// <param name="d">Facing.</param>
    public static int Dx(this Direction d) => d switch { Direction.East => 1, Direction.West => -1, _ => 0 };

    /// <summary>Y component of a unit step in this direction.</summary>
    /// <param name="d">Facing.</param>
    public static int Dy(this Direction d) => d switch { Direction.South => 1, Direction.North => -1, _ => 0 };

    /// <summary>Single-letter abbreviation (N, E, S, W).</summary>
    /// <param name="d">Facing.</param>
    public static char Letter(this Direction d) => d.ToString()[0];
}
