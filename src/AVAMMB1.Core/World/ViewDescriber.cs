using AVAMMB1.Core.Content;
using AVAMMB1.Core.Rules;
using AVAMMB1.Core.Session;

namespace AVAMMB1.Core.World;

/// <summary>
/// The party's surroundings in words - for players who cannot see the 3D view well (screen readers
/// read the log), and for anyone groping through the dark.
/// </summary>
public static class ViewDescriber
{
    /// <summary>A full description: where, which way, what lies ahead and to the sides, what can be seen.</summary>
    /// <param name="s">Session.</param>
    public static List<string> Describe(GameSession s)
    {
        var state = s.State;
        var map = s.CurrentMap;
        var lines = new List<string>();
        var when = map.Def.Kind is MapKind.Outdoor or MapKind.Town ? $" It is {state.PartOfDay}." : "";
        lines.Add($"{map.Def.Name}, square {state.X},{state.Y}, facing {state.Facing}.{when}");
        var sight = s.ViewDistance;
        if (s.IsDarkHere)
        {
            lines.Add(map.IsDarkness(state.X, state.Y) ? "Magical darkness swallows every light: you can feel only the walls around you." : "It is pitch dark: you can make out only what is within reach.");
        }
        lines.Add("Ahead: " + Line(s, state.Facing, sight) + ".");
        lines.Add($"Left: {Side(s, state.Facing.Left())}. Right: {Side(s, state.Facing.Right())}. Behind: {Side(s, state.Facing.Opposite())}.");

        var here = map.EventsAt(state.X, state.Y).Where(e => Visible(s, map, e)).Select(Name).Distinct().ToList();
        if (here.Count > 0)
        {
            lines.Add("Here: " + string.Join(", ", here) + ".");
        }
        var seen = new List<string>();
        var clear = OpenAhead(s, state.Facing, sight);
        foreach (var e in map.AllEvents.Where(e => (e.X, e.Y) != (state.X, state.Y) && Visible(s, map, e)))
        {
            var (ahead, lateral) = Relative(state.Facing, e.X - state.X, e.Y - state.Y);
            var inLine = lateral == 0 && ahead <= clear + 1; // straight down the open way (or the square just past it)
            if (ahead < 1 || ahead > sight || Math.Abs(lateral) > ahead || !(inLine || state.IsExplored(map.Id, map.Width, e.X, e.Y)))
            {
                continue;
            }
            var where = ahead == 1 && lateral == 0 ? "right in front of you"
                : $"{ahead} square{(ahead == 1 ? "" : "s")} ahead" + (lateral == 0 ? "" : $", {Math.Abs(lateral)} to the {(lateral < 0 ? "left" : "right")}");
            seen.Add($"{Name(e)} ({where})");
        }
        if (seen.Count > 0)
        {
            lines.Add("You can see: " + string.Join("; ", seen.Take(6)) + ".");
        }
        return lines;
    }

    /// <summary>A short line for describing each step: what is ahead, and which ways are open.</summary>
    /// <param name="s">Session.</param>
    public static string Brief(GameSession s)
    {
        var f = s.State.Facing;
        var open = new[] { (f.Left(), "left"), (f.Right(), "right") }.Where(x => s.CanPass(s.State.X, s.State.Y, x.Item1)).Select(x => x.Item2).ToList();
        return $"{f}: {Line(s, f, s.ViewDistance)}" + (open.Count > 0 ? $"; open to the {string.Join(" and ", open)}" : "") + ".";
    }

    /// <summary>How many squares are open straight ahead (within sight).</summary>
    private static int OpenAhead(GameSession s, Direction d, int sight)
    {
        var (x, y) = (s.State.X, s.State.Y);
        var steps = 0;
        while (steps < Math.Max(1, sight) && s.CanPass(x, y, d) && !s.CurrentMap.Probe(x, y, d).DestinationSolid)
        {
            (x, y) = (x + d.Dx(), y + d.Dy());
            steps++;
        }
        return steps;
    }

    /// <summary>"open for 3 squares, then a door" / "a wall".</summary>
    private static string Line(GameSession s, Direction d, int sight)
    {
        var map = s.CurrentMap;
        var (x, y) = (s.State.X, s.State.Y);
        var steps = 0;
        while (steps < Math.Max(1, sight))
        {
            var (wall, solid) = map.Probe(x, y, d);
            if (solid || wall != WallKind.None || !s.CanPass(x, y, d))
            {
                var what = solid ? Solid(map, x + d.Dx(), y + d.Dy()) : Wall(s, x, y, d, wall);
                return steps == 0 ? what : $"open for {steps} square{(steps == 1 ? "" : "s")}, then {what}";
            }
            (x, y) = (x + d.Dx(), y + d.Dy());
            steps++;
        }
        return $"open for at least {steps} square{(steps == 1 ? "" : "s")}";
    }

    private static string Side(GameSession s, Direction d)
    {
        var (wall, solid) = s.CurrentMap.Probe(s.State.X, s.State.Y, d);
        return solid ? Solid(s.CurrentMap, s.State.X + d.Dx(), s.State.Y + d.Dy()) : wall == WallKind.None ? "open" : Wall(s, s.State.X, s.State.Y, d, wall);
    }

    /// <summary>"water", "a building", "the map's edge".</summary>
    private static string Solid(GameMap map, int x, int y)
    {
        if (!map.InBounds(x, y))
        {
            return "the edge of the map";
        }
        var name = map.Terrain(x, y)?.Name;
        if (string.IsNullOrEmpty(name))
        {
            return map.Def.Kind == MapKind.Dungeon ? "solid rock" : "something in the way";
        }
        var n = name.ToLowerInvariant();
        return n.EndsWith('s') || n.Contains("water", StringComparison.Ordinal) ? n : "a " + n; // "mountains", "water", "a building"
    }

    private static string Wall(GameSession s, int x, int y, Direction d, WallKind wall) => wall switch
    {
        WallKind.Door => "a door",
        WallKind.LockedDoor => s.CanPass(x, y, d) ? "a door (unlocked)" : "a locked door",
        WallKind.SecretDoor => s.State.IsSecretFound(s.CurrentMap.Id, x, y, d) ? "a secret door" : "a wall",
        _ => "a wall",
    };

    /// <summary>Events worth naming: places, stairs, chests, features and guardians - not hidden traps or spinners.</summary>
    private static bool Visible(GameSession s, GameMap map, MapEventDef e)
    {
        if (e.Type is MapEventKind.Trap or MapEventKind.Spinner || (e.Type == MapEventKind.Message && e.Feature is null) || e.Name is null && e.Feature is null)
        {
            return false;
        }
        var key = e.Id ?? $"{map.Id}:{e.X}:{e.Y}:{map.Def.Events.IndexOf(e)}";
        return !(e.Once && s.State.CompletedEvents.Contains(key)) && !(e.OpenWhenMet && s.RequirementsMet(e));
    }

    private static string Name(MapEventDef e)
    {
        if (e.Type == MapEventKind.Encounter && e.Monsters.Count > 0)
        {
            return (e.Name ?? "a guardian") + " - something stands guard";
        }
        var name = e.Name ?? e.Feature!.Replace('_', ' ');
        return e.Type switch
        {
            MapEventKind.Treasure => $"{name} (treasure)",
            MapEventKind.Teleport => $"{name} (a way out)",
            MapEventKind.Riddle => $"{name} (a riddle)",
            _ => name,
        };
    }

    /// <summary>Offsets relative to the facing: squares ahead, and squares to the right (negative: left).</summary>
    private static (int Ahead, int Right) Relative(Direction f, int dx, int dy) => f switch
    {
        Direction.North => (-dy, dx),
        Direction.South => (dy, -dx),
        Direction.East => (dx, dy),
        _ => (-dx, -dy),
    };
}
