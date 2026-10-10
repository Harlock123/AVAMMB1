using AVAMMB1.Core.Content;
using AVAMMB1.Core.Dice;
using AVAMMB1.Core.Rules;

namespace AVAMMB1.Core.World;

/// <summary>
/// The Depths Below: an endless post-game dungeon below the Sunless Deep. Each level is a 16x16 maze
/// generated from a seed (kept in the save), with monsters, loot and gold that grow with depth.
/// </summary>
public static class Depths
{
    /// <summary>The map id every depth level uses (regenerated on each descent).</summary>
    public const string MapId = "depths";

    /// <summary>Where the stair up leads: beside the Depths stair in the Sunless Deep.</summary>
    public const string EntranceMap = "deep";

    /// <summary>The Sunless Deep square of the stair down (and where the party returns, beside it).</summary>
    public static readonly (int X, int Y) EntranceStair = (15, 0), ReturnSquare = (14, 0);

    /// <summary>The flag that opens the stair (the Umbral Wyrm is dead).</summary>
    public const string OpenFlag = "umbral_wyrm_slain";

    private const int Size = 16;

    private static readonly (string Wall, string Floor)[] Looks =
    [
        ("wall_obsidian", "floor_cobalt"), ("wall_deep", "floor_crypt"), ("wall_catacombs", "floor_tomb"),
        ("wall_vault", "floor_marble"), ("wall_belfry_deep", "floor_crypt"), ("wall_ice_deep", "floor_ice"),
    ];

    /// <summary>Monster strength (hit points and damage) and rewards, in percent: +10% a level below the third.</summary>
    /// <param name="depth">Depth (1 and up).</param>
    public static int ScalePercent(int depth) => 100 + 10 * Math.Max(0, depth - 3);

    /// <summary>The level of the monsters met at a depth (the hardest ordinary monsters, then scaled).</summary>
    /// <param name="depth">Depth.</param>
    public static int MonsterLevel(int depth) => Math.Min(15, 12 + depth);

    /// <summary>The seed of a daily challenge level: the same for everyone on that date.</summary>
    /// <param name="date">The day ("2026-10-10").</param>
    /// <param name="depth">Depth.</param>
    public static int DailySeed(string date, int depth)
    {
        var h = 17;
        foreach (var ch in date)
        {
            h = unchecked(h * 31 + ch); // a fixed hash (string.GetHashCode differs from run to run)
        }
        return unchecked(h * 7919 + depth * 104729) & 0x7FFFFFFF;
    }

    /// <summary>The square the party arrives on (the stair up is there).</summary>
    public static (int X, int Y) Start => (0, Size - 1);

    /// <summary>Generates one level of the Depths.</summary>
    /// <param name="db">Content (monsters and items to place).</param>
    /// <param name="depth">Depth (1 and up).</param>
    /// <param name="seed">Seed (the same seed and depth always give the same level).</param>
    public static MapDef Generate(ContentDatabase db, int depth, int seed)
    {
        var rng = new Random(seed ^ (depth * 7919));
        var (hw, vw) = Maze(rng);
        var doors = new HashSet<(char, int, int)>();
        // Two small rooms break up the corridors.
        for (var r = 0; r < 2; r++)
        {
            var x0 = rng.Next(2, Size - 5);
            var y0 = rng.Next(2, Size - 5);
            for (var y = y0; y <= y0 + 2; y++)
            {
                for (var x = x0; x <= x0 + 2; x++)
                {
                    if (x < x0 + 2) vw[y, x + 1] = false;
                    if (y < y0 + 2) hw[y + 1, x] = false;
                }
            }
        }
        var dist = Distances(hw, vw, Start);
        var deadEnds = DeadEnds(hw, vw).Where(c => c != Start).OrderByDescending(c => dist[c.Y, c.X]).ToList();
        var down = deadEnds.Count > 0 ? deadEnds[0] : (Size - 1, 0);
        var spots = deadEnds.Skip(1).OrderBy(_ => rng.Next()).ToList();
        var darkCells = new HashSet<(int, int)>();
        if (depth >= 3)
        {
            var c = (rng.Next(3, Size - 3), rng.Next(3, Size - 3));
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    darkCells.Add((c.Item1 + dx, c.Item2 + dy));
                }
            }
            darkCells.Remove(down);
            darkCells.Remove(Start);
        }

        var level = MonsterLevel(depth);
        var pool = db.Monsters.Values.Where(m => !m.Boss && m.Level >= level - 3 && m.Level <= level && m.Drops.Count == 0)
            .OrderBy(m => m.Id, StringComparer.Ordinal).ToList();
        if (pool.Count == 0)
        {
            pool = db.Monsters.Values.Where(m => !m.Boss).OrderByDescending(m => m.Level).Take(6).ToList();
        }
        var table = pool.OrderBy(_ => rng.Next()).Take(6).Select(m => new EncounterEntryDef { Monster = m.Id, Count = DiceExpression.Parse(m.Level >= level - 1 ? "1d2" : "1d3+1"), Weight = 10 }).ToList();
        var loot = db.Items.Values.Where(i => i.Kind is not (ItemKind.Quest or ItemKind.Food) && i.Price >= 300 * depth && i.Price <= 2500 * depth + 2000)
            .OrderBy(i => i.Id, StringComparer.Ordinal).ToList();
        string LootItem() => loot.Count > 0 ? loot[rng.Next(loot.Count)].Id : "potion_vigor";

        var look = Looks[(depth - 1) % Looks.Length];
        var events = new List<MapEventDef>
        {
            new() { X = Start.X, Y = Start.Y, Type = MapEventKind.Teleport, Name = "Stair Up", Feature = "stairs_up", Map = EntranceMap,
                ToX = ReturnSquare.X, ToY = ReturnSquare.Y, Facing = Direction.West,
                Text = depth == 1 ? "You climb back up into the Sunless Deep." : $"You climb all the way back up to the Sunless Deep. (Level {depth} is lost to you - the Depths shift behind you.)" },
            new() { X = down.Item1, Y = down.Item2, Type = MapEventKind.Teleport, Name = $"Stair Down to Level {depth + 1}", Feature = "stairs_down", Map = MapId,
                ToX = Start.X, ToY = Start.Y, Facing = Direction.North,
                Text = $"The stair winds down into the dark. Level {depth + 1} of the Depths Below." },
        };
        var k = 0;
        foreach (var (x, y) in spots.Take(3))
        {
            events.Add(new MapEventDef
            {
                Id = $"depths{depth}_chest{k++}", X = x, Y = y, Type = MapEventKind.Treasure, Once = true, Feature = "chest", Name = "Forgotten Hoard",
                Gold = DiceExpression.Parse($"{8 + depth * 2}d20+{100 * depth}"), Gems = 1 + depth / 3, Items = [LootItem(), "potion_vigor"],
                Text = "Treasure left by those who came this far before you.",
            });
        }
        if (spots.Count > 3 && table.Count > 0)
        {
            var (gx, gy) = spots[3];
            var guard = pool[rng.Next(pool.Count)];
            events.Add(new MapEventDef
            {
                Id = $"depths{depth}_guard", X = gx, Y = gy, Type = MapEventKind.Encounter, Once = true, Name = "Guardians of the Deep",
                Text = "Something has made its lair here, and does not want to share it.",
                Monsters = [new FixedMonsterDef { Monster = guard.Id, Count = DiceExpression.Parse("3") }],
            });
            events.Add(new MapEventDef
            {
                Id = $"depths{depth}_guard_loot", X = gx, Y = gy, Type = MapEventKind.Treasure, Once = true, Feature = "chest", Name = "Lair Hoard",
                RequiresFlag = $"depths{depth}_guard_slain", Gold = DiceExpression.Parse($"{10 + depth * 3}d20+{200 * depth}"), Gems = 2 + depth / 2,
                Items = [LootItem()], Text = "The lair's hoard.",
            });
            events[^2].SetFlag = $"depths{depth}_guard_slain";
        }
        foreach (var (x, y) in spots.Skip(4).Take(2))
        {
            events.Add(new MapEventDef
            {
                Id = $"depths{depth}_trap{k++}", X = x, Y = y, Type = MapEventKind.Trap, Once = true,
                Damage = DiceExpression.Parse($"{3 + depth}d8"), Text = "The floor gives way to a shaft of jagged stone!",
            });
        }
        if (depth % 3 == 0 && spots.Count > 6)
        {
            var (fx, fy) = spots[6];
            events.Add(new MapEventDef { X = fx, Y = fy, Type = MapEventKind.Fountain, Name = "A Spring in the Dark", Feature = "fountain", Heal = true, RestoreSp = true,
                Text = "Clear water wells up from the rock. It tastes of nothing at all, and heals every wound." });
        }

        return new MapDef
        {
            Id = MapId,
            Name = $"The Depths Below, level {depth}",
            Kind = MapKind.Dungeon,
            Format = MapFormat.Edges,
            Width = Size,
            Height = Size,
            Grid = Render(hw, vw, doors, darkCells),
            Terrain = new Dictionary<string, TerrainDef> { ["d"] = new() { Darkness = true, Name = "Magical darkness", MapColor = "#07070c" } },
            WallTexture = look.Wall,
            FloorTexture = look.Floor,
            CeilingTexture = look.Wall,
            Dark = true,
            Music = "dungeon",
            Ambience = "deep",
            EncounterChance = 9,
            Encounters = table,
            Events = events,
        };
    }

    /// <summary>hw[y, x]: wall on the north edge of cell (x, y); vw[y, x]: wall on its west edge.</summary>
    private static (bool[,] Hw, bool[,] Vw) Maze(Random rng)
    {
        var hw = new bool[Size + 1, Size];
        var vw = new bool[Size, Size + 1];
        for (var y = 0; y <= Size; y++) for (var x = 0; x < Size; x++) hw[y, x] = true;
        for (var y = 0; y < Size; y++) for (var x = 0; x <= Size; x++) vw[y, x] = true;
        var seen = new bool[Size, Size];
        var stack = new Stack<(int X, int Y)>();
        stack.Push(Start);
        seen[Start.Y, Start.X] = true;
        while (stack.Count > 0)
        {
            var (x, y) = stack.Peek();
            var options = new List<(int, int)>();
            foreach (var (dx, dy) in new[] { (0, -1), (1, 0), (0, 1), (-1, 0) })
            {
                var (nx, ny) = (x + dx, y + dy);
                if (nx >= 0 && ny >= 0 && nx < Size && ny < Size && !seen[ny, nx])
                {
                    options.Add((nx, ny));
                }
            }
            if (options.Count == 0)
            {
                stack.Pop();
                continue;
            }
            var (cx, cy) = options[rng.Next(options.Count)];
            Open(hw, vw, x, y, cx, cy);
            seen[cy, cx] = true;
            stack.Push((cx, cy));
        }
        // Loops: knock out about one wall in eight so the maze has more than one way round.
        for (var i = 0; i < Size * Size / 8; i++)
        {
            var x = rng.Next(1, Size);
            var y = rng.Next(1, Size);
            if (rng.Next(2) == 0) hw[y, x - 1] = false; else vw[y - 1, x] = false;
        }
        return (hw, vw);
    }

    private static void Open(bool[,] hw, bool[,] vw, int x, int y, int nx, int ny)
    {
        if (nx == x) hw[Math.Max(y, ny), x] = false;
        else vw[y, Math.Max(x, nx)] = false;
    }

    private static IEnumerable<(int X, int Y)> Neighbours(bool[,] hw, bool[,] vw, int x, int y)
    {
        if (y > 0 && !hw[y, x]) yield return (x, y - 1);
        if (y < Size - 1 && !hw[y + 1, x]) yield return (x, y + 1);
        if (x > 0 && !vw[y, x]) yield return (x - 1, y);
        if (x < Size - 1 && !vw[y, x + 1]) yield return (x + 1, y);
    }

    private static int[,] Distances(bool[,] hw, bool[,] vw, (int X, int Y) from)
    {
        var d = new int[Size, Size];
        for (var y = 0; y < Size; y++) for (var x = 0; x < Size; x++) d[y, x] = -1;
        var q = new Queue<(int X, int Y)>();
        q.Enqueue(from);
        d[from.Y, from.X] = 0;
        while (q.Count > 0)
        {
            var (x, y) = q.Dequeue();
            foreach (var (nx, ny) in Neighbours(hw, vw, x, y))
            {
                if (d[ny, nx] < 0)
                {
                    d[ny, nx] = d[y, x] + 1;
                    q.Enqueue((nx, ny));
                }
            }
        }
        return d;
    }

    private static List<(int X, int Y)> DeadEnds(bool[,] hw, bool[,] vw)
    {
        var list = new List<(int, int)>();
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                if (Neighbours(hw, vw, x, y).Count() == 1)
                {
                    list.Add((x, y));
                }
            }
        }
        return list;
    }

    /// <summary>The edges text format: '+' corners, '-'/'|' walls, 'D' doors, '.' (or a zone letter) for cells.</summary>
    private static List<string> Render(bool[,] hw, bool[,] vw, HashSet<(char, int, int)> doors, HashSet<(int, int)> dark)
    {
        var rows = new List<string>();
        for (var y = 0; y <= Size; y++)
        {
            var line = new System.Text.StringBuilder();
            for (var x = 0; x < Size; x++)
            {
                line.Append('+').Append(doors.Contains(('h', x, y)) ? 'D' : hw[y, x] ? '-' : ' ');
            }
            rows.Add(line.Append('+').ToString());
            if (y == Size)
            {
                break;
            }
            line.Clear();
            for (var x = 0; x <= Size; x++)
            {
                line.Append(doors.Contains(('v', x, y)) ? 'D' : vw[y, x] ? '|' : ' ');
                if (x < Size)
                {
                    line.Append(dark.Contains((x, y)) ? 'd' : '.');
                }
            }
            rows.Add(line.ToString());
        }
        return rows;
    }
}
