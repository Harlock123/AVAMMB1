using AVAMMB1.Core.Dice;
using AVAMMB1.Core.Rules;

namespace AVAMMB1.Core.Content;

/// <summary>The broad type of a map, which drives rendering, music and rules.</summary>
public enum MapKind
{
    /// <summary>A town with services.</summary>
    Town,
    /// <summary>The open wilderness.</summary>
    Outdoor,
    /// <summary>An underground or indoor dungeon.</summary>
    Dungeon,
}

/// <summary>How the <see cref="MapDef.Grid"/> is encoded.</summary>
public enum MapFormat
{
    /// <summary>
    /// Thin-wall format: (2H+1) rows of (2W+1) characters. Cells sit at odd row/column positions,
    /// edges between them at even positions (<c>|</c>/<c>-</c> wall, <c>D</c> door, <c>L</c> locked door, <c>S</c> secret door).
    /// </summary>
    Edges,
    /// <summary>Block format: H rows of W characters, one character per cell.</summary>
    Blocks,
}

/// <summary>Kinds of scripted map events.</summary>
public enum MapEventKind
{
    /// <summary>Displays a message.</summary>
    Message,
    /// <summary>Opens a shop.</summary>
    Shop,
    /// <summary>Inn: rest, save and manage the roster.</summary>
    Inn,
    /// <summary>Temple: healing, curing, resurrection.</summary>
    Temple,
    /// <summary>Tavern: food, drinks and rumors.</summary>
    Tavern,
    /// <summary>Training grounds: level up.</summary>
    Training,
    /// <summary>Academy: pay to raise a statistic permanently.</summary>
    Academy,
    /// <summary>Moves the party to another map or location.</summary>
    Teleport,
    /// <summary>Grants treasure.</summary>
    Treasure,
    /// <summary>Starts a fixed combat encounter.</summary>
    Encounter,
    /// <summary>Damages the party unless avoided.</summary>
    Trap,
    /// <summary>Heals or restores the party.</summary>
    Fountain,
    /// <summary>Quest step: checks requirements, sets flags and grants rewards.</summary>
    Quest,
    /// <summary>Ends the game in victory.</summary>
    Victory,
    /// <summary>Silently turns the party to a random facing.</summary>
    Spinner,
}

/// <summary>What a <see cref="MapEventKind.Trap"/> does when it is not disarmed.</summary>
public enum TrapEffect
{
    /// <summary>Damages every member (with a saving throw for half) and may inflict <see cref="MapEventDef.Conditions"/>.</summary>
    Damage,
    /// <summary>Teleports the party to <see cref="MapEventDef.Map"/>/<see cref="MapEventDef.ToX"/>/<see cref="MapEventDef.ToY"/>, or to a random open square.</summary>
    Teleport,
    /// <summary>Damage, then a fall to the destination (as <see cref="Teleport"/>).</summary>
    Pit,
    /// <summary>Summons monsters: <see cref="MapEventDef.Monsters"/>, or a group from the map's encounter table.</summary>
    Alarm,
}

/// <summary>A fixed group of monsters for an event-driven encounter.</summary>
public sealed class FixedMonsterDef
{
    /// <summary>Monster id.</summary>
    public string Monster { get; set; } = "";
    /// <summary>How many.</summary>
    public DiceExpression Count { get; set; } = new(0, 0, 1);
}

/// <summary>A scripted event placed on a map cell.</summary>
public sealed class MapEventDef
{
    /// <summary>Optional unique id (used to remember one-shot events).</summary>
    public string? Id { get; set; }
    /// <summary>Cell X.</summary>
    public int X { get; set; }
    /// <summary>Cell Y.</summary>
    public int Y { get; set; }
    /// <summary>Event kind.</summary>
    public MapEventKind Type { get; set; }
    /// <summary>Title / building name.</summary>
    public string? Name { get; set; }
    /// <summary>Main text.</summary>
    public string? Text { get; set; }
    /// <summary>Text shown when requirements are not met.</summary>
    public string? FailText { get; set; }
    /// <summary>Shop id for <see cref="MapEventKind.Shop"/>.</summary>
    public string? Shop { get; set; }
    /// <summary>Destination map for teleports.</summary>
    public string? Map { get; set; }
    /// <summary>Destination X.</summary>
    public int ToX { get; set; }
    /// <summary>Destination Y.</summary>
    public int ToY { get; set; }
    /// <summary>Gold charged for a teleport (a ferry fare); paid by the party, travel fails if it can't pay.</summary>
    public int Fare { get; set; }
    /// <summary>Destination facing.</summary>
    public Direction? Facing { get; set; }
    /// <summary>Gold reward.</summary>
    public DiceExpression Gold { get; set; }
    /// <summary>Gem reward.</summary>
    public int Gems { get; set; }
    /// <summary>Experience reward (per living member).</summary>
    public int Xp { get; set; }
    /// <summary>Item rewards.</summary>
    public List<string> Items { get; set; } = new();
    /// <summary>Monsters for encounters.</summary>
    public List<FixedMonsterDef> Monsters { get; set; } = new();
    /// <summary>Trap damage per member.</summary>
    public DiceExpression Damage { get; set; }
    /// <summary>What a trap does (default: damage).</summary>
    public TrapEffect Trap { get; set; }
    /// <summary>Condition inflicted by traps or cured by fountains.</summary>
    public Condition Conditions { get; set; }
    /// <summary>Flag that must be set for the event to fire.</summary>
    public string? RequiresFlag { get; set; }
    /// <summary>Flag that must NOT be set for the event to fire.</summary>
    public string? RequiresNotFlag { get; set; }
    /// <summary>Item that must be carried by someone in the party.</summary>
    public string? RequiresItem { get; set; }
    /// <summary>Remove the required item when the event fires.</summary>
    public bool ConsumeItem { get; set; }
    /// <summary>Flag to set when the event fires.</summary>
    public string? SetFlag { get; set; }
    /// <summary>Fires only once per game.</summary>
    public bool Once { get; set; }
    /// <summary>If true, the event blocks movement into the cell when requirements fail.</summary>
    public bool Blocking { get; set; }
    /// <summary>Billboard sprite drawn in the 3D view (Graphics/Features).</summary>
    public string? Feature { get; set; }
    /// <summary>Price multiplier for services.</summary>
    public double PriceFactor { get; set; } = 1.0;
    /// <summary>Rumors offered by taverns.</summary>
    public List<string> Rumors { get; set; } = new();
    /// <summary>Whether a fountain heals hit points.</summary>
    public bool Heal { get; set; }
    /// <summary>Whether a fountain restores spell points.</summary>
    public bool RestoreSp { get; set; }
}

/// <summary>Terrain legend entry used by the map grid.</summary>
public sealed class TerrainDef
{
    /// <summary>Blocks movement; drawn as a block unless <see cref="Opaque"/> is false.</summary>
    public bool Solid { get; set; }
    /// <summary>Whether the cell blocks sight (defaults to <see cref="Solid"/>). Non-opaque solid cells (trees, water) show their floor and feature instead.</summary>
    public bool? Opaque { get; set; }
    /// <summary>Texture key for solid blocks.</summary>
    public string? Texture { get; set; }
    /// <summary>Optional floor texture override for walkable terrain.</summary>
    public string? Floor { get; set; }
    /// <summary>Optional billboard drawn in the cell.</summary>
    public string? Feature { get; set; }
    /// <summary>Name shown on the automap legend.</summary>
    public string? Name { get; set; }
    /// <summary>Automap color hex (e.g. <c>#2a6</c>).</summary>
    public string? MapColor { get; set; }
    /// <summary>Magical darkness: light sources and spells do nothing here.</summary>
    public bool Darkness { get; set; }
    /// <summary>Anti-magic: no spells, scrolls or wands work here (for monsters either).</summary>
    public bool AntiMagic { get; set; }
}

/// <summary>A map as stored in JSON (see docs/CONTENT_FORMAT.md).</summary>
public sealed class MapDef
{
    /// <summary>Unique id.</summary>
    public string Id { get; set; } = "";
    /// <summary>Display name.</summary>
    public string Name { get; set; } = "";
    /// <summary>Map kind.</summary>
    public MapKind Kind { get; set; }
    /// <summary>Grid encoding.</summary>
    public MapFormat Format { get; set; }
    /// <summary>Width in cells.</summary>
    public int Width { get; set; }
    /// <summary>Height in cells.</summary>
    public int Height { get; set; }
    /// <summary>The grid rows (see <see cref="MapFormat"/>).</summary>
    public List<string> Grid { get; set; } = new();
    /// <summary>Terrain legend keyed by single character.</summary>
    public Dictionary<string, TerrainDef> Terrain { get; set; } = new();
    /// <summary>Default wall texture.</summary>
    public string WallTexture { get; set; } = "wall_dungeon";
    /// <summary>Default floor texture.</summary>
    public string FloorTexture { get; set; } = "floor_dirt";
    /// <summary>Ceiling texture; <c>null</c> draws a sky gradient.</summary>
    public string? CeilingTexture { get; set; }
    /// <summary>Sky top color (outdoor/town).</summary>
    public string SkyColor { get; set; } = "#3b6fb6";
    /// <summary>Whether the map is dark without a light source.</summary>
    public bool Dark { get; set; }
    /// <summary>Music track key.</summary>
    public string Music { get; set; } = "dungeon";
    /// <summary>Looping ambient sound key (Assets/Audio/Ambience), or null for none.</summary>
    public string? Ambience { get; set; }
    /// <summary>Chance per step of a random encounter (percent).</summary>
    public int EncounterChance { get; set; }
    /// <summary>Random encounter table.</summary>
    public List<EncounterEntryDef> Encounters { get; set; } = new();
    /// <summary>Outdoor maps: monsters that also roam at night (half of night encounters come from this list).</summary>
    public List<EncounterEntryDef> NightEncounters { get; set; } = new();
    /// <summary>Item id that opens locked doors on this map.</summary>
    public string? LockedDoorKey { get; set; }
    /// <summary>Flag that opens locked doors on this map.</summary>
    public string? LockedDoorFlag { get; set; }
    /// <summary>Scripted events.</summary>
    public List<MapEventDef> Events { get; set; } = new();
}
