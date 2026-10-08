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
}

/// <summary>A fixed group of monsters for an event-driven encounter.</summary>
public sealed class FixedMonsterDef
{
    /// <summary>Monster id.</summary>
    public string Monster { get; init; } = "";
    /// <summary>How many.</summary>
    public DiceExpression Count { get; init; } = new(0, 0, 1);
}

/// <summary>A scripted event placed on a map cell.</summary>
public sealed class MapEventDef
{
    /// <summary>Optional unique id (used to remember one-shot events).</summary>
    public string? Id { get; init; }
    /// <summary>Cell X.</summary>
    public int X { get; init; }
    /// <summary>Cell Y.</summary>
    public int Y { get; init; }
    /// <summary>Event kind.</summary>
    public MapEventKind Type { get; init; }
    /// <summary>Title / building name.</summary>
    public string? Name { get; init; }
    /// <summary>Main text.</summary>
    public string? Text { get; init; }
    /// <summary>Text shown when requirements are not met.</summary>
    public string? FailText { get; init; }
    /// <summary>Shop id for <see cref="MapEventKind.Shop"/>.</summary>
    public string? Shop { get; init; }
    /// <summary>Destination map for teleports.</summary>
    public string? Map { get; init; }
    /// <summary>Destination X.</summary>
    public int ToX { get; init; }
    /// <summary>Destination Y.</summary>
    public int ToY { get; init; }
    /// <summary>Destination facing.</summary>
    public Direction? Facing { get; init; }
    /// <summary>Gold reward.</summary>
    public DiceExpression Gold { get; init; }
    /// <summary>Gem reward.</summary>
    public int Gems { get; init; }
    /// <summary>Experience reward (per living member).</summary>
    public int Xp { get; init; }
    /// <summary>Item rewards.</summary>
    public List<string> Items { get; init; } = new();
    /// <summary>Monsters for encounters.</summary>
    public List<FixedMonsterDef> Monsters { get; init; } = new();
    /// <summary>Trap damage per member.</summary>
    public DiceExpression Damage { get; init; }
    /// <summary>Condition inflicted by traps or cured by fountains.</summary>
    public Condition Conditions { get; init; }
    /// <summary>Flag that must be set for the event to fire.</summary>
    public string? RequiresFlag { get; init; }
    /// <summary>Flag that must NOT be set for the event to fire.</summary>
    public string? RequiresNotFlag { get; init; }
    /// <summary>Item that must be carried by someone in the party.</summary>
    public string? RequiresItem { get; init; }
    /// <summary>Remove the required item when the event fires.</summary>
    public bool ConsumeItem { get; init; }
    /// <summary>Flag to set when the event fires.</summary>
    public string? SetFlag { get; init; }
    /// <summary>Fires only once per game.</summary>
    public bool Once { get; init; }
    /// <summary>If true, the event blocks movement into the cell when requirements fail.</summary>
    public bool Blocking { get; init; }
    /// <summary>Billboard sprite drawn in the 3D view (Graphics/Features).</summary>
    public string? Feature { get; init; }
    /// <summary>Price multiplier for services.</summary>
    public double PriceFactor { get; init; } = 1.0;
    /// <summary>Rumors offered by taverns.</summary>
    public List<string> Rumors { get; init; } = new();
    /// <summary>Whether a fountain heals hit points.</summary>
    public bool Heal { get; init; }
    /// <summary>Whether a fountain restores spell points.</summary>
    public bool RestoreSp { get; init; }
}

/// <summary>Terrain legend entry used by the map grid.</summary>
public sealed class TerrainDef
{
    /// <summary>Blocks movement and sight; drawn as a block.</summary>
    public bool Solid { get; init; }
    /// <summary>Texture key for solid blocks.</summary>
    public string? Texture { get; init; }
    /// <summary>Optional floor texture override for walkable terrain.</summary>
    public string? Floor { get; init; }
    /// <summary>Optional billboard drawn in the cell.</summary>
    public string? Feature { get; init; }
    /// <summary>Name shown on the automap legend.</summary>
    public string? Name { get; init; }
    /// <summary>Automap color hex (e.g. <c>#2a6</c>).</summary>
    public string? MapColor { get; init; }
}

/// <summary>A map as stored in JSON (see docs/CONTENT_FORMAT.md).</summary>
public sealed class MapDef
{
    /// <summary>Unique id.</summary>
    public string Id { get; init; } = "";
    /// <summary>Display name.</summary>
    public string Name { get; init; } = "";
    /// <summary>Map kind.</summary>
    public MapKind Kind { get; init; }
    /// <summary>Grid encoding.</summary>
    public MapFormat Format { get; init; }
    /// <summary>Width in cells.</summary>
    public int Width { get; init; }
    /// <summary>Height in cells.</summary>
    public int Height { get; init; }
    /// <summary>The grid rows (see <see cref="MapFormat"/>).</summary>
    public List<string> Grid { get; init; } = new();
    /// <summary>Terrain legend keyed by single character.</summary>
    public Dictionary<string, TerrainDef> Terrain { get; init; } = new();
    /// <summary>Default wall texture.</summary>
    public string WallTexture { get; init; } = "wall_dungeon";
    /// <summary>Default floor texture.</summary>
    public string FloorTexture { get; init; } = "floor_dirt";
    /// <summary>Ceiling texture; <c>null</c> draws a sky gradient.</summary>
    public string? CeilingTexture { get; init; }
    /// <summary>Sky top color (outdoor/town).</summary>
    public string SkyColor { get; init; } = "#3b6fb6";
    /// <summary>Whether the map is dark without a light source.</summary>
    public bool Dark { get; init; }
    /// <summary>Music track key.</summary>
    public string Music { get; init; } = "dungeon";
    /// <summary>Chance per step of a random encounter (percent).</summary>
    public int EncounterChance { get; init; }
    /// <summary>Random encounter table.</summary>
    public List<EncounterEntryDef> Encounters { get; init; } = new();
    /// <summary>Item id that opens locked doors on this map.</summary>
    public string? LockedDoorKey { get; init; }
    /// <summary>Flag that opens locked doors on this map.</summary>
    public string? LockedDoorFlag { get; init; }
    /// <summary>Scripted events.</summary>
    public List<MapEventDef> Events { get; init; } = new();
}
