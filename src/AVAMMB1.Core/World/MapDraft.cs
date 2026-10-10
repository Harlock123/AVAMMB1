using System.Text.Json;
using AVAMMB1.Core.Content;
using AVAMMB1.Core.Persistence;

namespace AVAMMB1.Core.World;

/// <summary>A side of a cell, for editing walls.</summary>
public enum CellSide
{
    /// <summary>North (towards y - 1).</summary>
    North,
    /// <summary>East (towards x + 1).</summary>
    East,
    /// <summary>South (towards y + 1).</summary>
    South,
    /// <summary>West (towards x - 1).</summary>
    West,
}

/// <summary>
/// A map being edited in the map editor: cells, the walls and doors between them, and events. It reads
/// any map (a "blocks" map becomes an "edges" one with the same squares) and writes the "edges" format
/// of <c>Maps/*.json</c> (see docs/CONTENT_FORMAT.md).
/// </summary>
public sealed class MapDraft
{
    /// <summary>Largest width or height.</summary>
    public const int MaxSize = 64;

    private MapDef _meta;
    private char[,] _cells;
    private char[,] _hEdges; // [x, y] = the edge on the north side of row y (y = Height is the south border)
    private char[,] _vEdges; // [x, y] = the edge on the west side of column x (x = Width is the east border)

    private MapDraft(MapDef meta, int width, int height)
    {
        _meta = meta;
        Width = width;
        Height = height;
        _cells = new char[width, height];
        _hEdges = new char[width, height + 1];
        _vEdges = new char[width + 1, height];
        Fill();
    }

    private void Fill()
    {
        for (var x = 0; x < Width; x++)
        {
            for (var y = 0; y < Height; y++)
            {
                _cells[x, y] = '.';
            }
        }
        for (var x = 0; x < Width; x++)
        {
            for (var y = 0; y <= Height; y++)
            {
                _hEdges[x, y] = y == 0 || y == Height ? '-' : ' ';
            }
        }
        for (var x = 0; x <= Width; x++)
        {
            for (var y = 0; y < Height; y++)
            {
                _vEdges[x, y] = x == 0 || x == Width ? '|' : ' ';
            }
        }
    }

    /// <summary>Width in cells.</summary>
    public int Width { get; private set; }

    /// <summary>Height in cells.</summary>
    public int Height { get; private set; }

    /// <summary>Map settings (id, name, kind, textures, music, encounters...). Grid, size and events are the draft's own.</summary>
    public MapDef Meta => _meta;

    /// <summary>The events, in order.</summary>
    public List<MapEventDef> Events { get; private set; } = [];

    /// <summary>A new map with walls all round.</summary>
    /// <param name="id">Map id.</param>
    /// <param name="name">Name shown in the game.</param>
    /// <param name="kind">Kind.</param>
    /// <param name="width">Width.</param>
    /// <param name="height">Height.</param>
    public static MapDraft New(string id, string name, MapKind kind, int width, int height)
    {
        var meta = new MapDef
        {
            Id = id,
            Name = name,
            Kind = kind,
            Format = MapFormat.Edges,
            Dark = kind == MapKind.Dungeon,
            Music = kind switch { MapKind.Town => "town", MapKind.Outdoor => "title", _ => "dungeon" },
            WallTexture = kind switch { MapKind.Town => "wall_town", MapKind.Outdoor => "wall_mountain", _ => "wall_dungeon" },
            FloorTexture = kind switch { MapKind.Town => "floor_cobble", MapKind.Outdoor => "floor_grass", _ => "floor_dirt" },
            CeilingTexture = kind == MapKind.Dungeon ? "wall_dungeon" : null,
            EncounterChance = kind == MapKind.Town ? 0 : 4,
        };
        return new MapDraft(meta, Math.Clamp(width, 2, MaxSize), Math.Clamp(height, 2, MaxSize));
    }

    /// <summary>A draft of an existing map (a copy: the original is not changed).</summary>
    /// <param name="def">The map.</param>
    public static MapDraft From(MapDef def)
    {
        var copy = Clone(def);
        var parsed = GameMap.Parse(copy);
        var draft = new MapDraft(copy, def.Width, def.Height);
        for (var x = 0; x < def.Width; x++)
        {
            for (var y = 0; y < def.Height; y++)
            {
                draft._cells[x, y] = parsed.CellChar(x, y);
            }
        }
        if (def.Format == MapFormat.Edges)
        {
            for (var r = 0; r < def.Grid.Count; r++)
            {
                var row = def.Grid[r].PadRight(def.Width * 2 + 1);
                for (var c = 0; c < row.Length; c++)
                {
                    var ch = EdgeChar(row[c]);
                    if ((r & 1) == 0 && (c & 1) == 1)
                    {
                        draft._hEdges[c / 2, r / 2] = ch == '|' ? '-' : ch;
                    }
                    else if ((r & 1) == 1 && (c & 1) == 0)
                    {
                        draft._vEdges[c / 2, r / 2] = ch == '-' ? '|' : ch;
                    }
                }
            }
        }
        else
        {
            // A blocks map has no walls between squares (not even round the edge): solid squares do the walling.
            for (var x = 0; x < def.Width; x++)
            {
                for (var y = 0; y <= def.Height; y++)
                {
                    draft._hEdges[x, y] = ' ';
                }
            }
            for (var x = 0; x <= def.Width; x++)
            {
                for (var y = 0; y < def.Height; y++)
                {
                    draft._vEdges[x, y] = ' ';
                }
            }
        }
        draft.Events = copy.Events;
        copy.Events = [];
        copy.Grid = [];
        copy.Format = MapFormat.Edges;
        return draft;
    }

    private static char EdgeChar(char ch) => ch switch
    {
        '-' or '|' or '#' => ch == '|' ? '|' : '-',
        'D' or 'L' or 'S' => ch,
        _ => ' ',
    };

    private static MapDef Clone(MapDef def) =>
        JsonSerializer.Deserialize(JsonSerializer.Serialize(def, GameJsonContext.Default.MapDef), GameJsonContext.Default.MapDef)!;

    /// <summary>Whether a cell is inside the map.</summary>
    /// <param name="x">X.</param>
    /// <param name="y">Y.</param>
    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    /// <summary>A cell's character ('.' floor, '#' solid rock, or a terrain legend key).</summary>
    /// <param name="x">X.</param>
    /// <param name="y">Y.</param>
    public char Cell(int x, int y) => InBounds(x, y) ? _cells[x, y] : '#';

    /// <summary>Sets a cell's character.</summary>
    /// <param name="x">X.</param>
    /// <param name="y">Y.</param>
    /// <param name="ch">'.', '#' or a terrain legend key.</param>
    public void SetCell(int x, int y, char ch)
    {
        if (InBounds(x, y) && ch is not (' ' or '+' or '-' or '|'))
        {
            _cells[x, y] = ch;
        }
    }

    /// <summary>The edge on one side of a cell: ' ' open, '-' or '|' wall, 'D' door, 'L' locked door, 'S' secret door.</summary>
    /// <param name="x">X.</param>
    /// <param name="y">Y.</param>
    /// <param name="side">Side.</param>
    public char Edge(int x, int y, CellSide side) => side switch
    {
        CellSide.North => _hEdges[x, y],
        CellSide.South => _hEdges[x, y + 1],
        CellSide.West => _vEdges[x, y],
        _ => _vEdges[x + 1, y],
    };

    /// <summary>Sets the edge on one side of a cell (shared with the neighbour). The outer border stays walled unless it gets a door.</summary>
    /// <param name="x">X.</param>
    /// <param name="y">Y.</param>
    /// <param name="side">Side.</param>
    /// <param name="kind">' ' open, 'W' or '-' or '|' wall, 'D', 'L' or 'S'.</param>
    public void SetEdge(int x, int y, CellSide side, char kind)
    {
        if (!InBounds(x, y))
        {
            return;
        }
        var border = side switch
        {
            CellSide.North => y == 0,
            CellSide.South => y == Height - 1,
            CellSide.West => x == 0,
            _ => x == Width - 1,
        };
        var horizontal = side is CellSide.North or CellSide.South;
        var ch = kind switch
        {
            'W' or '-' or '|' or '#' => horizontal ? '-' : '|',
            'D' or 'L' or 'S' => kind,
            _ => border ? (horizontal ? '-' : '|') : ' ',
        };
        switch (side)
        {
            case CellSide.North: _hEdges[x, y] = ch; break;
            case CellSide.South: _hEdges[x, y + 1] = ch; break;
            case CellSide.West: _vEdges[x, y] = ch; break;
            default: _vEdges[x + 1, y] = ch; break;
        }
    }

    /// <summary>Walls a cell in on every side (or opens every side).</summary>
    /// <param name="x">X.</param>
    /// <param name="y">Y.</param>
    /// <param name="walled">Walls or openings.</param>
    public void Box(int x, int y, bool walled)
    {
        foreach (var side in Enum.GetValues<CellSide>())
        {
            SetEdge(x, y, side, walled ? 'W' : ' ');
        }
    }

    /// <summary>The events on a cell.</summary>
    /// <param name="x">X.</param>
    /// <param name="y">Y.</param>
    public IEnumerable<MapEventDef> EventsAt(int x, int y) => Events.Where(e => e.X == x && e.Y == y);

    /// <summary>Changes the size, keeping what fits (new space is open floor; the new border is walled).</summary>
    /// <param name="width">New width.</param>
    /// <param name="height">New height.</param>
    public void Resize(int width, int height)
    {
        width = Math.Clamp(width, 2, MaxSize);
        height = Math.Clamp(height, 2, MaxSize);
        var (oldCells, oldH, oldV, oldW, oldHgt) = (_cells, _hEdges, _vEdges, Width, Height);
        (Width, Height) = (width, height);
        _cells = new char[width, height];
        _hEdges = new char[width, height + 1];
        _vEdges = new char[width + 1, height];
        Fill();
        for (var x = 0; x < Math.Min(width, oldW); x++)
        {
            for (var y = 0; y < Math.Min(height, oldHgt); y++)
            {
                _cells[x, y] = oldCells[x, y];
                if (y > 0)
                {
                    _hEdges[x, y] = oldH[x, y];
                }
                if (x > 0)
                {
                    _vEdges[x, y] = oldV[x, y];
                }
            }
        }
        Events.RemoveAll(e => !InBounds(e.X, e.Y));
    }

    /// <summary>The map as it would be saved ("edges" format).</summary>
    public MapDef ToMapDef()
    {
        var def = Clone(_meta);
        def.Format = MapFormat.Edges;
        def.Width = Width;
        def.Height = Height;
        def.Events = Events.Select(e => JsonSerializer.Deserialize(JsonSerializer.Serialize(e, GameJsonContext.Default.MapEventDef), GameJsonContext.Default.MapEventDef)!).ToList();
        var rows = new List<string>();
        for (var y = 0; y <= Height; y++)
        {
            var top = new System.Text.StringBuilder("+");
            for (var x = 0; x < Width; x++)
            {
                top.Append(_hEdges[x, y]).Append('+');
            }
            rows.Add(top.ToString());
            if (y == Height)
            {
                break;
            }
            var mid = new System.Text.StringBuilder();
            for (var x = 0; x < Width; x++)
            {
                mid.Append(_vEdges[x, y]).Append(_cells[x, y]);
            }
            mid.Append(_vEdges[Width, y]);
            rows.Add(mid.ToString());
        }
        def.Grid = rows;
        return def;
    }

    /// <summary>The map file's JSON (indented, as in Assets/Data/Maps; values left at their defaults are left out).</summary>
    public string ToJson()
    {
        var node = JsonSerializer.SerializeToNode(ToMapDef(), GameJsonContext.Default.MapDef)!;
        Tidy(node, inTerrain: false);
        return node.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\n";
    }

    /// <summary>An event as tidy JSON (for editing by hand in the map editor).</summary>
    /// <param name="e">The event.</param>
    public static string EventToJson(MapEventDef e)
    {
        var node = JsonSerializer.SerializeToNode(e, GameJsonContext.Default.MapEventDef)!;
        Tidy(node, inTerrain: false);
        return node.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    }

    /// <summary>Reads an event written as JSON.</summary>
    /// <param name="json">JSON.</param>
    /// <exception cref="InvalidDataException">Not an event.</exception>
    public static MapEventDef EventFromJson(string json)
    {
        try
        {
            return JsonSerializer.Deserialize(json, GameJsonContext.Default.MapEventDef) ?? throw new InvalidDataException("Empty event.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(ex.Message, ex);
        }
    }

    private static readonly HashSet<string> AlwaysKept = ["x", "y", "type", "opaque", "width", "height", "grid", "id", "name", "kind", "format", "events"];

    /// <summary>Drops properties left at their defaults (0, false, "", "0", empty lists, a price factor of 1).</summary>
    private static void Tidy(System.Text.Json.Nodes.JsonNode node, bool inTerrain)
    {
        switch (node)
        {
            case System.Text.Json.Nodes.JsonObject obj:
                foreach (var (key, value) in obj.ToList())
                {
                    if (value is not null)
                    {
                        Tidy(value, inTerrain || key == "terrain");
                    }
                    if (AlwaysKept.Contains(key) || (inTerrain && key == "solid"))
                    {
                        continue;
                    }
                    var drop = value switch
                    {
                        null => true,
                        System.Text.Json.Nodes.JsonArray a => a.Count == 0,
                        System.Text.Json.Nodes.JsonObject o => o.Count == 0 && key != "terrain",
                        System.Text.Json.Nodes.JsonValue v when v.TryGetValue<bool>(out var b) => !b,
                        System.Text.Json.Nodes.JsonValue v when v.TryGetValue<double>(out var d) => d == 0 || (key == "priceFactor" && d == 1),
                        System.Text.Json.Nodes.JsonValue v when v.TryGetValue<string>(out var s) => s.Length == 0
                            || (s == "0" && key is "gold" or "damage" or "penalty")
                            || (s == "None" && key == "conditions") || (s == "Damage" && key == "trap" && obj["type"]?.GetValue<string>() != "Trap"),
                        _ => false,
                    };
                    if (drop)
                    {
                        obj.Remove(key);
                    }
                }
                break;
            case System.Text.Json.Nodes.JsonArray arr:
                foreach (var item in arr)
                {
                    if (item is not null)
                    {
                        Tidy(item, inTerrain);
                    }
                }
                break;
        }
    }

    /// <summary>Reads a map file.</summary>
    /// <param name="json">JSON.</param>
    /// <exception cref="InvalidDataException">Not a valid map.</exception>
    public static MapDraft FromJson(string json)
    {
        try
        {
            var def = JsonSerializer.Deserialize(json, GameJsonContext.Default.MapDef) ?? throw new InvalidDataException("Empty map file.");
            return From(def);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Not a map file: " + ex.Message, ex);
        }
    }

    /// <summary>
    /// What is wrong with the map, in plain words (empty when it is fine): unknown monsters, shops, items or
    /// destinations, events no one can reach from the first event or the given start, and an id clash.
    /// </summary>
    /// <param name="content">The game content (for references).</param>
    /// <param name="startX">Where the party arrives (for reachability), or -1 to start from the first teleport event.</param>
    /// <param name="startY">Start Y.</param>
    public IReadOnlyList<string> Problems(ContentDatabase content, int startX = -1, int startY = -1)
    {
        var problems = new List<string>();
        if (string.IsNullOrWhiteSpace(_meta.Id) || _meta.Id.Any(ch => !(char.IsAsciiLetterOrDigit(ch) || ch is '_' or '-')))
        {
            problems.Add("The map id may only use letters, digits, '-' and '_'.");
        }
        GameMap map;
        try
        {
            map = GameMap.Parse(ToMapDef());
        }
        catch (InvalidDataException ex)
        {
            problems.Add(ex.Message);
            return problems;
        }
        foreach (var e in _meta.Encounters.Concat(_meta.NightEncounters).Where(e => !content.Monsters.ContainsKey(e.Monster)))
        {
            problems.Add($"Unknown monster in the encounters: {e.Monster}");
        }
        foreach (var e in Events)
        {
            var where = $"{e.Type} at ({e.X},{e.Y})";
            foreach (var m in e.Monsters.Where(m => !content.Monsters.ContainsKey(m.Monster)))
            {
                problems.Add($"{where}: unknown monster {m.Monster}");
            }
            foreach (var i in e.Items.Concat(e.RequiresItem is { } r ? [r] : []).Where(i => !content.Items.ContainsKey(i)))
            {
                problems.Add($"{where}: unknown item {i}");
            }
            if (e.Type == MapEventKind.Shop && (e.Shop is null || !content.Shops.ContainsKey(e.Shop)))
            {
                problems.Add($"{where}: unknown shop {e.Shop}");
            }
            if (e.Type == MapEventKind.Teleport && e.Map is { } to && to != _meta.Id && !content.Maps.ContainsKey(to))
            {
                problems.Add($"{where}: unknown map {to}");
            }
            if (map.IsSolid(e.X, e.Y))
            {
                problems.Add($"{where}: the square is solid");
            }
        }
        var start = startX >= 0 ? (startX, startY) : Events.FirstOrDefault(e => e.Type == MapEventKind.Teleport) is { } t ? (t.X, t.Y) : (-1, -1);
        if (start.Item1 >= 0 && map.InBounds(start.Item1, start.Item2))
        {
            var reach = map.Reachable(start.Item1, start.Item2);
            foreach (var e in Events.Where(e => !reach.Contains((e.X, e.Y))))
            {
                problems.Add($"{e.Type} at ({e.X},{e.Y}) cannot be reached from ({start.Item1},{start.Item2})");
            }
        }
        return problems;
    }
}
