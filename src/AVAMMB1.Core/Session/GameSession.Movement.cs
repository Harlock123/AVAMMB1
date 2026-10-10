using AVAMMB1.Core.Characters;
using AVAMMB1.Core.Combat;
using AVAMMB1.Core.Content;
using AVAMMB1.Core.Dice;
using AVAMMB1.Core.Items;
using AVAMMB1.Core.Magic;
using AVAMMB1.Core.Rules;
using AVAMMB1.Core.World;

namespace AVAMMB1.Core.Session;

/// <summary>Moving, resting, searching, time passing and light.</summary>
public sealed partial class GameSession
{
    /// <summary>Turns the party left.</summary>
    public void TurnLeft()
    {
        State.Facing = State.Facing.Left();
        if (IsActive)
        {
            Explore();
        }
    }

    /// <summary>Turns the party right.</summary>
    public void TurnRight()
    {
        State.Facing = State.Facing.Right();
        if (IsActive)
        {
            Explore();
        }
    }

    /// <summary>Moves the party.</summary>
    /// <param name="kind">Relative movement.</param>
    public StepResult Move(MoveKind kind)
    {
        var result = new StepResult();
        if (Combat is not null || !IsActive)
        {
            return result;
        }
        if (!State.Party.Any(c => c.CanAct))
        {
            result.Messages.Add(new("No one in the party is able to move!", MessageKind.Bad));
            return result;
        }
        var dir = kind switch
        {
            MoveKind.Back => State.Facing.Opposite(),
            MoveKind.StrafeLeft => State.Facing.Left(),
            MoveKind.StrafeRight => State.Facing.Right(),
            _ => State.Facing,
        };
        var map = CurrentMap;
        var (wall, solid) = map.Probe(State.X, State.Y, dir);
        if (solid || wall == WallKind.Wall || (wall == WallKind.SecretDoor && !State.IsSecretFound(map.Id, State.X, State.Y, dir)))
        {
            result.Messages.Add(new(map.Def.Kind == MapKind.Dungeon ? "Ouch! A solid wall." : "The way is blocked.", MessageKind.Info, "bump"));
            return result;
        }
        if (wall == WallKind.LockedDoor && !CanOpenLocks(map) && !State.PickedLocks.Contains(GameState.SecretKey(map.Id, State.X, State.Y, dir)))
        {
            TryPickLock(map, dir, result);
            return result;
        }
        var nx = State.X + dir.Dx();
        var ny = State.Y + dir.Dy();
        foreach (var ev in map.EventsAt(nx, ny))
        {
            if (ev.Blocking && !IsCompleted(map, ev) && !RequirementsMet(ev))
            {
                result.Messages.Add(new(ev.FailText ?? "Something bars the way.", MessageKind.Story, "bump"));
                return result;
            }
        }

        var wasDark = map.IsDarkness(State.X, State.Y);
        var wasAntiMagic = map.IsAntiMagic(State.X, State.Y);
        State.X = nx;
        State.Y = ny;
        result.Moved = true;
        if (map.IsDarkness(nx, ny) && !wasDark)
        {
            result.Messages.Add(new("An unnatural darkness swallows every light.", MessageKind.Bad));
        }
        if (map.IsAntiMagic(nx, ny) && !wasAntiMagic)
        {
            result.Messages.Add(new("The air turns dead and still. Your magic falls silent.", MessageKind.Bad));
        }
        if (wall is WallKind.Door or WallKind.LockedDoor or WallKind.SecretDoor)
        {
            result.Messages.Add(new(wall == WallKind.LockedDoor ? "The lock clicks open." : "", MessageKind.Info, "door"));
        }
        else
        {
            result.Messages.Add(new("", MessageKind.Info, StepSound(map, nx, ny)));
        }
        result.Messages.RemoveAll(m => m.Text.Length == 0 && m.Sound is null);
        PassTime(1, result.Messages);
        Explore();
        TriggerEvents(result, entering: true);
        if (!result.CombatStarted && result.Interaction is null && !result.MapChanged && !result.Victory)
        {
            TryRandomEncounter(result, CurrentMap.Def.EncounterChance);
        }
        return result;
    }

    /// <summary>Re-uses the current cell (re-enter a shop, read a sign again).</summary>
    public StepResult Interact()
    {
        var result = new StepResult();
        if (Combat is not null || !IsActive)
        {
            return result;
        }
        TriggerEvents(result, entering: false);
        if (result.Messages.Count == 0 && result.Interaction is null && !result.CombatStarted && result.StoryText is null)
        {
            result.Messages.Add(new("There is nothing of interest here.", MessageKind.Info));
        }
        return result;
    }

    /// <summary>Rests for eight hours: eats food, restores HP and SP. May be interrupted by monsters.</summary>
    public StepResult Rest()
    {
        var result = new StepResult();
        if (Combat is not null || !IsActive)
        {
            return result;
        }
        var map = CurrentMap;
        if (map.Def.EncounterChance > 0 && Random.Chance(Math.Min(40, map.Def.EncounterChance * 4)))
        {
            result.Messages.Add(new("The party's rest is interrupted!", MessageKind.Bad, "roar"));
            TryRandomEncounter(result, 100);
            if (result.CombatStarted)
            {
                return result;
            }
        }
        PassTime(50, result.Messages);
        State.Minutes += 8 * 60 - 50 * GameState.MinutesPerStep; // a rest is eight hours on the clock
        foreach (var c in State.Party.Where(c => c.IsAlive))
        {
            RestCharacter(c, result.Messages, requireFood: true);
        }
        State.LastMealMinutes = State.Minutes; // the rest's meal counts as the day's ration
        result.Messages.Insert(0, new("The party rests for eight hours and shares a meal (1 food each).", MessageKind.Info));
        return result;
    }

    /// <summary>Rest logic for one character (used by camping and the inn).</summary>
    /// <param name="c">Character.</param>
    /// <param name="log">Messages.</param>
    /// <param name="requireFood">Whether a food unit is consumed.</param>
    public void RestCharacter(Character c, List<GameMessage> log, bool requireFood)
    {
        c.Conditions &= ~Condition.Asleep;
        if (requireFood)
        {
            if (c.Food <= 0)
            {
                log.Add(new($"{c.Name} has no food and cannot recover.", MessageKind.Bad));
                return;
            }
            c.Food--;
        }
        c.Sp = c.MaxSp;
        if (c.Has(Condition.Poisoned))
        {
            log.Add(new($"{c.Name} is too sick with poison to heal.", MessageKind.Bad));
            return;
        }
        var amount = c.Has(Condition.Diseased) ? (c.MaxHp - Math.Max(c.Hp, 0)) / 2 : c.MaxHp;
        if (c.Has(Condition.Unconscious) && c.Hp <= 0)
        {
            c.Hp = 0;
        }
        Rulebook.Heal(c, Math.Max(1, amount));
    }

    /// <summary>Uses an item or casts a spell while exploring (handles Recall teleport).</summary>
    /// <param name="r">Result from <see cref="SpellCaster"/>.</param>
    public StepResult AfterExploreMagic(SpellResult r)
    {
        var result = new StepResult();
        result.Messages.AddRange(r.Messages);
        if (r.Teleported)
        {
            result.MapChanged = true;
            Explore();
        }
        return result;
    }

    /// <summary>
    /// Whether the party could step from a cell in a direction right now, ignoring map events:
    /// walls, solid terrain, locked doors (needs the map's key or flag) and undiscovered secret doors block.
    /// </summary>
    /// <param name="x">Cell X.</param>
    /// <param name="y">Cell Y.</param>
    /// <param name="dir">Direction of travel.</param>
    public bool CanPass(int x, int y, Direction dir)
    {
        var map = CurrentMap;
        var (wall, solid) = map.Probe(x, y, dir);
        return !solid && wall switch
        {
            WallKind.Wall => false,
            WallKind.LockedDoor => CanOpenLocks(map) || State.PickedLocks.Contains(GameState.SecretKey(map.Id, x, y, dir)),
            WallKind.SecretDoor => State.IsSecretFound(map.Id, x, y, dir),
            _ => true,
        };
    }

    /// <summary>Chance (percent) that a party member spots a hidden door when searching.</summary>
    /// <param name="c">Searcher.</param>
    public int SearchChance(Character c) =>
        Math.Clamp(35 + 10 * (Rules.Bonus(c, Stat.Intellect) + Rules.Bonus(c, Stat.Luck)) + Rules.Thievery(c) / 2, 20, 95);

    /// <summary>
    /// Searches the walls around the party for secret doors. Takes a few minutes of game time and,
    /// like a step, may attract wandering monsters. The best searcher in the party rolls once per hidden door.
    /// </summary>
    public StepResult Search()
    {
        var result = new StepResult();
        if (Combat is not null || !IsActive)
        {
            return result;
        }
        var searcher = State.Party.Where(c => c.CanAct).OrderByDescending(SearchChance).FirstOrDefault();
        if (searcher is null)
        {
            result.Messages.Add(new("No one in the party is able to search!", MessageKind.Bad));
            return result;
        }
        var map = CurrentMap;
        result.Messages.Add(new($"{searcher.Name} searches the walls carefully...", MessageKind.Info, "step"));
        PassTime(5, result.Messages);
        var found = 0;
        foreach (var d in Enum.GetValues<Direction>())
        {
            if (map.GetWall(State.X, State.Y, d) == WallKind.SecretDoor &&
                !State.IsSecretFound(map.Id, State.X, State.Y, d) &&
                Random.Chance(SearchChance(searcher)))
            {
                State.FoundSecrets.Add(GameState.SecretKey(map.Id, State.X, State.Y, d));
                result.Messages.Add(new($"{searcher.Name} discovers a secret door to the {DescribeSide(d)}!", MessageKind.Good, "door"));
                State.Count(Chronicle.Keys.Secrets);
                found++;
            }
        }
        if (found == 0)
        {
            result.Messages.Add(new("You find nothing unusual.", MessageKind.Info));
            // Under the open sky, monsters are half as likely again to find you at night.
            TryRandomEncounter(result, IsNightOutside && map.Def.Kind == MapKind.Outdoor ? map.Def.EncounterChance * 3 / 2 : map.Def.EncounterChance);
        }
        return result;
    }

    private string DescribeSide(Direction d) =>
        d == State.Facing ? "front" : d == State.Facing.Opposite() ? "rear" : d == State.Facing.Left() ? "left" : "right";

    /// <summary>The footstep sound for the ground at a square: water, snow, soft earth outdoors, or stone.</summary>
    /// <param name="map">Map.</param>
    /// <param name="x">X.</param>
    /// <param name="y">Y.</param>
    public static string StepSound(GameMap map, int x, int y)
    {
        var floor = map.FloorTexture(x, y);
        return floor switch
        {
            "water" or "shallow_water" => "step_water",
            "floor_snow" or "floor_ice" => "step_snow",
            "floor_grass" or "floor_earth" or "floor_dirt" or "floor_bog" or "floor_sand" when map.Def.Kind != MapKind.Dungeon => "step_soft",
            _ => "step",
        };
    }

    /// <summary>The party's best lock-picker tries a locked door (robbers); a failed try takes a few minutes.</summary>
    private void TryPickLock(GameMap map, Direction dir, StepResult result)
    {
        var picker = State.Party.Where(c => c.IsAlive && c.CanAct && Rules.HasAbility(c, ClassAbility.PickLocks))
            .OrderByDescending(Rules.LockpickChance).FirstOrDefault();
        if (picker is null)
        {
            result.Messages.Add(new("The door is locked tight.", MessageKind.Info, "bump"));
            return;
        }
        if (map.Def.MasterLocks)
        {
            result.Messages.Add(new($"{picker.Name} studies the lock and shakes their head: no pick will open this one. It needs its key.", MessageKind.Info, "bump"));
            return;
        }
        var chance = Rules.LockpickChance(picker);
        PassTime(5, result.Messages);
        if (Random.Chance(chance))
        {
            State.PickedLocks.Add(GameState.SecretKey(map.Id, State.X, State.Y, dir));
            State.Count(Chronicle.Keys.Locks);
            result.Messages.Add(new($"{picker.Name} works the lock - click! The door is open.", MessageKind.Good, "door"));
        }
        else
        {
            result.Messages.Add(new($"{picker.Name} fails to pick the lock ({chance}% chance). Try again?", MessageKind.Info, "lockpick"));
            TryRandomEncounter(result, map.Def.EncounterChance);
        }
    }

    private bool CanOpenLocks(GameMap map) =>
        (map.Def.LockedDoorKey is { } key && Inventory_AnyoneHas(key)) ||
        (map.Def.LockedDoorFlag is { } flag && State.Flags.Contains(flag));

    private bool Inventory_AnyoneHas(string itemId) => Items.Inventory.AnyoneHas(State.Party, itemId);

    private void PassTime(int steps, List<GameMessage> log)
    {
        var before = State.Steps;
        State.Steps += steps;
        State.Minutes += steps * GameState.MinutesPerStep;
        State.LightSteps = Math.Max(0, State.LightSteps - steps);
        BurnLantern(steps, log);
        DailyRations(log);
        var ticks = (int)(State.Steps / 10 - before / 10);
        if (ticks <= 0)
        {
            return;
        }
        foreach (var c in State.Party.Where(c => c.IsAlive && c.Has(Condition.Poisoned)))
        {
            if (Rules.ApplyDamage(c, ticks))
            {
                log.Add(new(c.Has(Condition.Dead) ? $"{c.Name} dies of poison!" : $"{c.Name} collapses from poison!", MessageKind.Bad));
            }
        }
    }

    /// <summary>Survival mode: one food each per day on the clock since the last meal; without it, hunger.</summary>
    private void DailyRations(List<GameMessage> log)
    {
        if (!State.Survival)
        {
            State.LastMealMinutes = State.Minutes; // switching survival on later starts the day fed
            return;
        }
        while (State.Minutes - State.LastMealMinutes >= GameState.MinutesPerDay)
        {
            State.LastMealMinutes += GameState.MinutesPerDay;
            var hungry = new List<string>();
            foreach (var c in State.Party.Where(c => c.IsAlive))
            {
                if (c.Food > 0)
                {
                    c.Food--;
                    if (c.Food == 2)
                    {
                        log.Add(new($"{c.Name} is down to 2 days of food.", MessageKind.Bad));
                    }
                    continue;
                }
                // Hunger wears a character down but never kills: at worst it leaves them on 1 HP.
                var loss = Math.Min(Math.Max(1, c.MaxHp / 10), Math.Max(0, c.Hp - 1));
                c.Hp -= loss;
                hungry.Add(loss > 0 ? $"{c.Name} (-{loss} HP)" : c.Name);
            }
            log.Add(new("The party eats its daily rations.", MessageKind.Info));
            if (hungry.Count > 0)
            {
                log.Add(new($"No food! Hunger gnaws at {string.Join(", ", hungry)}. Buy food at a tavern.", MessageKind.Bad));
            }
        }
    }

    /// <summary>Steps of oil left at which the lantern warns that it is running low.</summary>
    public const int LanternLowWarning = 150;

    /// <summary>The lantern lighting the party burns oil while the party is in a dark place.</summary>
    private void BurnLantern(int steps, List<GameMessage> log)
    {
        var dark = CurrentMap.Def.Dark || (State.IsNight && CurrentMap.Def.Kind == MapKind.Outdoor); // towns have street lights
        if (!IsActive || !dark || Items.Lanterns.Active(Rules, State.Party) is not { } active)
        {
            return;
        }
        var (holder, lantern) = active;
        if (Rules.Def(lantern).FuelCapacity == 0)
        {
            return; // never needs oil
        }
        var before = lantern.Charges;
        lantern.Charges = Math.Max(0, lantern.Charges - steps);
        if (lantern.Charges == 0)
        {
            log.Add(new($"{holder.Name}'s lantern sputters and goes out. Fill it with a flask of oil.", MessageKind.Bad));
        }
        else if (before > LanternLowWarning && lantern.Charges <= LanternLowWarning)
        {
            log.Add(new($"{holder.Name}'s lantern is running low on oil ({lantern.Charges} steps left).", MessageKind.Bad));
        }
    }

    /// <summary>Marks the cells around the party as explored on the automap.</summary>
    public void Explore()
    {
        NoteCarriedItems();
        var map = CurrentMap;
        void Mark(int x, int y) => State.MarkExplored(map.Id, map.Width, map.Height, x, y);
        Mark(State.X, State.Y);
        foreach (var d in Enum.GetValues<Direction>())
        {
            var (wall, _) = map.Probe(State.X, State.Y, d);
            var hidden = wall == WallKind.SecretDoor && !State.IsSecretFound(map.Id, State.X, State.Y, d);
            if (map.Def.Kind != MapKind.Dungeon || (wall != WallKind.Wall && !hidden))
            {
                Mark(State.X + d.Dx(), State.Y + d.Dy());
            }
        }
        if (map.Def.Kind == MapKind.Dungeon && !IsDarkHere)
        {
            // The party also sees down the corridor ahead, as far as its light reaches.
            int x = State.X, y = State.Y;
            for (var i = 1; i < ViewDistance; i++)
            {
                var (wall, _) = map.Probe(x, y, State.Facing);
                if (wall != WallKind.None || !map.InBounds(x + State.Facing.Dx(), y + State.Facing.Dy()))
                {
                    break;
                }
                x += State.Facing.Dx();
                y += State.Facing.Dy();
                if (map.IsSolid(x, y))
                {
                    break;
                }
                Mark(x, y);
                foreach (var side in new[] { State.Facing.Left(), State.Facing.Right() })
                {
                    if (map.Probe(x, y, side).Wall == WallKind.None)
                    {
                        Mark(x + side.Dx(), y + side.Dy());
                    }
                }
            }
        }
        if (map.Def.Kind != MapKind.Dungeon)
        {
            for (var dy = -1; dy <= 1; dy += 2)
            {
                for (var dx = -1; dx <= 1; dx += 2)
                {
                    Mark(State.X + dx, State.Y + dy);
                }
            }
        }
    }
}
