using AVAMMB1.Core.Content;
using AVAMMB1.Core.Rules;

namespace AVAMMB1.Core.World;

/// <summary>What sits on the edge between two cells.</summary>
public enum WallKind : byte
{
    /// <summary>Open passage.</summary>
    None,
    /// <summary>Solid wall.</summary>
    Wall,
    /// <summary>Door (passable, blocks sight).</summary>
    Door,
    /// <summary>Locked door (needs the map key or flag).</summary>
    LockedDoor,
    /// <summary>Secret door: looks like a wall but can be walked through.</summary>
    SecretDoor,
}

/// <summary>A parsed, runtime map with fast wall/terrain lookups.</summary>
public sealed class GameMap
{
    private readonly WallKind[] _hEdges; // (H+1) * W : edge north of cell (x,y) at index y*W+x
    private readonly WallKind[] _vEdges; // H * (W+1) : edge west of cell (x,y) at index y*(W+1)+x
    private readonly char[] _cells;
    private readonly Dictionary<(int, int), List<MapEventDef>> _events = new();

    /// <summary>The source definition.</summary>
    public MapDef Def { get; }

    /// <summary>Map id.</summary>
    public string Id => Def.Id;

    /// <summary>Width in cells.</summary>
    public int Width { get; }

    /// <summary>Height in cells.</summary>
    public int Height { get; }

    private GameMap(MapDef def)
    {
        Def = def;
        Width = def.Width;
        Height = def.Height;
        _hEdges = new WallKind[(Height + 1) * Width];
        _vEdges = new WallKind[Height * (Width + 1)];
        _cells = new char[Width * Height];
        Array.Fill(_cells, '.');
    }

    /// <summary>Parses a map definition.</summary>
    /// <param name="def">Definition to parse.</param>
    /// <exception cref="InvalidDataException">Thrown when the grid dimensions are inconsistent.</exception>
    public static GameMap Parse(MapDef def)
    {
        if (def.Width <= 0 || def.Height <= 0)
        {
            throw new InvalidDataException($"Map '{def.Id}' has invalid size.");
        }
        var map = new GameMap(def);
        if (def.Format == MapFormat.Blocks)
        {
            Require(def, def.Grid.Count == def.Height, $"expected {def.Height} rows, found {def.Grid.Count}");
            for (var y = 0; y < def.Height; y++)
            {
                var row = def.Grid[y];
                Require(def, row.Length == def.Width, $"row {y} should have {def.Width} characters, found {row.Length}");
                for (var x = 0; x < def.Width; x++)
                {
                    map._cells[y * def.Width + x] = row[x];
                }
            }
        }
        else
        {
            Require(def, def.Grid.Count == def.Height * 2 + 1, $"expected {def.Height * 2 + 1} rows, found {def.Grid.Count}");
            for (var r = 0; r < def.Grid.Count; r++)
            {
                var row = def.Grid[r].PadRight(def.Width * 2 + 1);
                Require(def, row.Length == def.Width * 2 + 1, $"row {r} should have {def.Width * 2 + 1} characters, found {row.Length}");
                for (var c = 0; c < row.Length; c++)
                {
                    var ch = row[c];
                    var rowOdd = (r & 1) == 1;
                    var colOdd = (c & 1) == 1;
                    if (rowOdd && colOdd)
                    {
                        map._cells[(r / 2) * def.Width + c / 2] = ch == ' ' ? '.' : ch;
                    }
                    else if (!rowOdd && colOdd)
                    {
                        map._hEdges[(r / 2) * def.Width + c / 2] = ParseEdge(ch);
                    }
                    else if (rowOdd && !colOdd)
                    {
                        map._vEdges[(r / 2) * (def.Width + 1) + c / 2] = ParseEdge(ch);
                    }
                }
            }
        }

        foreach (var e in def.Events)
        {
            Require(def, map.InBounds(e.X, e.Y), $"event at ({e.X},{e.Y}) is out of bounds");
            if (!map._events.TryGetValue((e.X, e.Y), out var list))
            {
                map._events[(e.X, e.Y)] = list = new List<MapEventDef>();
            }
            list.Add(e);
        }
        return map;
    }

    private static void Require(MapDef def, bool ok, string message)
    {
        if (!ok)
        {
            throw new InvalidDataException($"Map '{def.Id}': {message}.");
        }
    }

    private static WallKind ParseEdge(char ch) => ch switch
    {
        '-' or '|' or '#' => WallKind.Wall,
        'D' => WallKind.Door,
        'L' => WallKind.LockedDoor,
        'S' => WallKind.SecretDoor,
        _ => WallKind.None,
    };

    /// <summary>Whether the coordinates are inside the map.</summary>
    /// <param name="x">Cell X.</param>
    /// <param name="y">Cell Y.</param>
    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    /// <summary>The terrain character at a cell (<c>'.'</c> for plain floor).</summary>
    /// <param name="x">Cell X.</param>
    /// <param name="y">Cell Y.</param>
    public char CellChar(int x, int y) => InBounds(x, y) ? _cells[y * Width + x] : '#';

    /// <summary>The terrain legend entry for a cell, if any.</summary>
    /// <param name="x">Cell X.</param>
    /// <param name="y">Cell Y.</param>
    public TerrainDef? Terrain(int x, int y) =>
        Def.Terrain.TryGetValue(CellChar(x, y).ToString(), out var t) ? t : null;

    /// <summary>Whether a cell is solid (blocks movement and sight). Out-of-bounds cells are solid.</summary>
    /// <param name="x">Cell X.</param>
    /// <param name="y">Cell Y.</param>
    public bool IsSolid(int x, int y)
    {
        if (!InBounds(x, y))
        {
            return true;
        }
        var ch = _cells[y * Width + x];
        if (ch == '#' && !Def.Terrain.ContainsKey("#"))
        {
            return true;
        }
        return Terrain(x, y)?.Solid == true;
    }

    /// <summary>Whether a cell blocks sight (solid blocks, unless the terrain says otherwise). Out-of-bounds cells are opaque.</summary>
    /// <param name="x">Cell X.</param>
    /// <param name="y">Cell Y.</param>
    public bool IsOpaque(int x, int y)
    {
        if (!InBounds(x, y))
        {
            return true;
        }
        var t = Terrain(x, y);
        return t?.Opaque ?? IsSolid(x, y);
    }

    /// <summary>Whether a cell is magically dark (light does not help there).</summary>
    /// <param name="x">Cell X.</param>
    /// <param name="y">Cell Y.</param>
    public bool IsDarkness(int x, int y) => Terrain(x, y)?.Darkness == true;

    /// <summary>Whether magic is suppressed in a cell.</summary>
    /// <param name="x">Cell X.</param>
    /// <param name="y">Cell Y.</param>
    public bool IsAntiMagic(int x, int y) => Terrain(x, y)?.AntiMagic == true;

    /// <summary>Texture key used to draw a solid cell.</summary>
    /// <param name="x">Cell X.</param>
    /// <param name="y">Cell Y.</param>
    public string SolidTexture(int x, int y) => Terrain(x, y)?.Texture ?? Def.WallTexture;

    /// <summary>Floor texture key for a cell.</summary>
    /// <param name="x">Cell X.</param>
    /// <param name="y">Cell Y.</param>
    public string FloorTexture(int x, int y) => Terrain(x, y)?.Floor ?? Def.FloorTexture;

    /// <summary>The wall on the given side of a cell.</summary>
    /// <param name="x">Cell X.</param>
    /// <param name="y">Cell Y.</param>
    /// <param name="side">Which side.</param>
    public WallKind GetWall(int x, int y, Direction side)
    {
        if (!InBounds(x, y))
        {
            return WallKind.Wall;
        }
        return side switch
        {
            Direction.North => _hEdges[y * Width + x],
            Direction.South => _hEdges[(y + 1) * Width + x],
            Direction.West => _vEdges[y * (Width + 1) + x],
            _ => _vEdges[y * (Width + 1) + x + 1],
        };
    }

    /// <summary>Events placed on a cell.</summary>
    /// <param name="x">Cell X.</param>
    /// <param name="y">Cell Y.</param>
    public IReadOnlyList<MapEventDef> EventsAt(int x, int y) =>
        _events.TryGetValue((x, y), out var list) ? list : Array.Empty<MapEventDef>();

    /// <summary>All events on the map.</summary>
    public IEnumerable<MapEventDef> AllEvents => Def.Events;

    /// <summary>
    /// Whether movement from a cell in a direction is physically possible, ignoring locks.
    /// Returns the wall kind encountered (None when free) and whether the destination is solid.
    /// </summary>
    /// <param name="x">Origin X.</param>
    /// <param name="y">Origin Y.</param>
    /// <param name="dir">Direction of travel.</param>
    public (WallKind Wall, bool DestinationSolid) Probe(int x, int y, Direction dir)
    {
        var wall = GetWall(x, y, dir);
        var nx = x + dir.Dx();
        var ny = y + dir.Dy();
        return (wall, IsSolid(nx, ny));
    }

    /// <summary>Returns the cells reachable from a start cell (locked doors treated as passable when <paramref name="throughLocks"/>).</summary>
    /// <param name="startX">Start X.</param>
    /// <param name="startY">Start Y.</param>
    /// <param name="throughLocks">Whether locked doors may be crossed.</param>
    public HashSet<(int X, int Y)> Reachable(int startX, int startY, bool throughLocks = true)
    {
        var seen = new HashSet<(int, int)>();
        var queue = new Queue<(int, int)>();
        queue.Enqueue((startX, startY));
        seen.Add((startX, startY));
        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            foreach (var d in Enum.GetValues<Direction>())
            {
                var (wall, solid) = Probe(x, y, d);
                if (solid || wall == WallKind.Wall || (!throughLocks && wall == WallKind.LockedDoor))
                {
                    continue;
                }
                var n = (x + d.Dx(), y + d.Dy());
                if (seen.Add(n))
                {
                    queue.Enqueue(n);
                }
            }
        }
        return seen;
    }
}
