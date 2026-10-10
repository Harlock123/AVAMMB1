using AVAMMB1.Core.Content;
using AVAMMB1.Core.Dice;
using AVAMMB1.Core.Rules;
using AVAMMB1.Core.World;

namespace AVAMMB1.Core.Session;

/// <summary>A town the party can travel to, and what the journey takes.</summary>
/// <param name="MapId">Town map id.</param>
/// <param name="Name">Town name.</param>
/// <param name="Steps">Steps on the road (time passes as if walked).</param>
/// <param name="Fare">Ship fares on the way.</param>
/// <param name="Via">The open country and ports crossed.</param>
public sealed record TravelOption(string MapId, string Name, int Steps, int Fare, IReadOnlyList<string> Via)
{
    /// <summary>About how long the journey takes: "about 5 hours", "about 2 days".</summary>
    public string Duration
    {
        get
        {
            var hours = Steps * GameState.MinutesPerStep / 60.0;
            return hours < 1 ? "under an hour" : hours < 24 ? $"about {Math.Round(hours)} hour{(Math.Round(hours) == 1 ? "" : "s")}" : $"about {Math.Round(hours / 24, 1)} days";
        }
    }
}

/// <summary>Travelling between known towns on the travel map.</summary>
public sealed partial class GameSession
{
    private sealed record Leg(string MapId, int X, int Y, MapEventDef? Exit, int Steps);

    /// <summary>Whether the party can use the travel map here (a town or the open country, not in battle).</summary>
    public bool CanTravel => Combat is null && IsActive && CurrentMap.Def.Kind is MapKind.Town or MapKind.Outdoor;

    /// <summary>The towns the party has visited and can reach from here, with the cost of each journey.</summary>
    public IReadOnlyList<TravelOption> TravelOptions()
    {
        if (!CanTravel)
        {
            return [];
        }
        var options = new List<TravelOption>();
        foreach (var town in Content.Maps.Values.Where(m => m.Def.Kind == MapKind.Town && m.Id != State.MapId && State.Explored.ContainsKey(m.Id)))
        {
            if (Plan(town.Id) is { } legs)
            {
                var via = legs.Skip(1).Select(l => Content.Map(l.MapId)).Where(m => m.Id != town.Id).Select(m => m.Def.Name).Distinct().ToList();
                options.Add(new TravelOption(town.Id, town.Def.Name, legs.Sum(l => l.Steps), legs.Sum(l => l.Exit?.Fare ?? 0), via));
            }
        }
        return options.OrderBy(o => o.Steps).ToList();
    }

    /// <summary>
    /// Travels to a known town: time passes as if the road were walked (with rations, oil and poison as
    /// usual), ship fares are paid, and each stretch of open country risks an ambush that ends the journey there.
    /// </summary>
    /// <param name="townId">Destination town.</param>
    public StepResult TravelTo(string townId)
    {
        var result = new StepResult();
        if (TravelOptions().FirstOrDefault(o => o.MapId == townId) is not { } option || Plan(townId) is not { } legs)
        {
            result.Messages.Add(new("You cannot travel there from here.", MessageKind.Info));
            return result;
        }
        if (State.Gold + State.Party.Sum(c => c.Gold) < option.Fare)
        {
            result.Messages.Add(new($"The voyage costs {option.Fare} gold in fares, which the party does not have.", MessageKind.Bad));
            return result;
        }
        result.Messages.Add(new($"The party sets out for {option.Name}.", MessageKind.Info, "step"));
        for (var i = 0; i < legs.Count; i++)
        {
            var leg = legs[i];
            var map = Content.Map(leg.MapId);
            (State.MapId, State.X, State.Y) = (leg.MapId, leg.X, leg.Y);
            PassTime(Math.Max(1, leg.Steps), result.Messages);
            if (leg.Exit is { Fare: > 0 } paid && State.TryPay(paid.Fare))
            {
                result.Messages.Add(new($"The party pays {paid.Fare} gold for passage.", MessageKind.Info, "coins"));
            }
            if (i > 0 && map.Def.Kind == MapKind.Outdoor && map.Def.Encounters.Count > 0 && Random.Chance(Math.Min(40, map.Def.EncounterChance * 3)))
            {
                result.Messages.Add(new($"On the road through {map.Def.Name}, the party is ambushed!", MessageKind.Bad, "roar"));
                result.MapChanged = true;
                Explore();
                TryRandomEncounter(result, 100);
                return result;
            }
        }
        var dest = legs[^1];
        (State.MapId, State.X, State.Y) = (dest.MapId, dest.X, dest.Y);
        result.MapChanged = true;
        result.Moved = true;
        result.Messages.Add(new($"After {option.Duration} on the road, the party arrives in {option.Name}.", MessageKind.Good));
        Explore();
        return result;
    }

    /// <summary>
    /// The cheapest route (fewest steps) from here to a town through towns and open country only, as legs:
    /// the square the party is on in each map, the passage it leaves by, and the steps walked there.
    /// </summary>
    private List<Leg>? Plan(string townId)
    {
        var start = (Map: State.MapId, State.X, State.Y);
        var best = new Dictionary<(string, int, int), (int Cost, (string, int, int)? Prev, MapEventDef? Exit, int Steps)> { [start] = (0, null, null, 0) };
        var open = new PriorityQueue<(string Map, int X, int Y), int>();
        open.Enqueue(start, 0);
        (string, int, int)? goal = null;
        while (open.TryDequeue(out var node, out var cost))
        {
            if (cost > best[node].Cost)
            {
                continue;
            }
            if (node.Map == townId)
            {
                goal = node;
                break;
            }
            var map = Content.Map(node.Map);
            var dist = Distances(map, node.X, node.Y);
            foreach (var exit in map.AllEvents.Where(e => e.Type == MapEventKind.Teleport && e.Map is { } to && to != node.Map && RequirementsMet(e)
                && Content.Maps.TryGetValue(to, out var next) && next.Def.Kind is MapKind.Town or MapKind.Outdoor))
            {
                if (!dist.TryGetValue((exit.X, exit.Y), out var steps))
                {
                    continue;
                }
                var next = (exit.Map!, exit.ToX, exit.ToY);
                var voyage = VoyageSteps(exit);
                var total = cost + steps + 1 + voyage + exit.Fare / 10; // a fare weighs a little against walking
                if (!best.TryGetValue(next, out var known) || total < known.Cost)
                {
                    best[next] = (total, node, exit, steps + 1 + voyage);
                    open.Enqueue(next, total);
                }
            }
        }
        if (goal is null)
        {
            return null;
        }
        // Walk back from the goal: each node's predecessor is where the party was before taking that passage.
        var legs = new List<Leg>();
        var at = goal.Value;
        legs.Add(new Leg(at.Item1, at.Item2, at.Item3, null, 0));
        while (best[at].Prev is { } prev)
        {
            legs.Add(new Leg(prev.Item1, prev.Item2, prev.Item3, best[at].Exit, best[at].Steps));
            at = prev;
        }
        legs.Reverse();
        return legs;
    }

    /// <summary>A paid passage (a ship or ferry) takes time at sea: two steps' worth for every gold of fare.</summary>
    private static int VoyageSteps(MapEventDef exit) => exit.Fare * 2;

    /// <summary>Walking distances (steps) from a square to every square reachable on a map.</summary>
    private Dictionary<(int, int), int> Distances(GameMap map, int x, int y)
    {
        var dist = new Dictionary<(int, int), int> { [(x, y)] = 0 };
        var q = new Queue<(int X, int Y)>([(x, y)]);
        while (q.Count > 0)
        {
            var (cx, cy) = q.Dequeue();
            foreach (var d in Enum.GetValues<Direction>())
            {
                var (wall, solid) = map.Probe(cx, cy, d);
                var n = (cx + d.Dx(), cy + d.Dy());
                if (solid || wall is WallKind.Wall || (wall == WallKind.LockedDoor && !CanOpenLocks(map)) || !map.InBounds(n.Item1, n.Item2) || dist.ContainsKey(n))
                {
                    continue;
                }
                dist[n] = dist[(cx, cy)] + 1;
                q.Enqueue(n);
            }
        }
        return dist;
    }
}
