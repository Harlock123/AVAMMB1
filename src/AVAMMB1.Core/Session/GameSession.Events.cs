using AVAMMB1.Core.Characters;
using AVAMMB1.Core.Combat;
using AVAMMB1.Core.Content;
using AVAMMB1.Core.Dice;
using AVAMMB1.Core.Items;
using AVAMMB1.Core.Magic;
using AVAMMB1.Core.Rules;
using AVAMMB1.Core.World;

namespace AVAMMB1.Core.Session;

/// <summary>Map events, traps and rewards.</summary>
public sealed partial class GameSession
{
    private static string EventKey(GameMap map, MapEventDef ev) =>
        ev.Id ?? $"{map.Id}:{ev.X}:{ev.Y}:{map.Def.Events.IndexOf(ev)}";

    private bool IsCompleted(GameMap map, MapEventDef ev) => ev.Once && State.CompletedEvents.Contains(EventKey(map, ev));

    /// <summary>Whether an event's flag and item requirements are satisfied.</summary>
    /// <param name="ev">Event.</param>
    public bool RequirementsMet(MapEventDef ev) =>
        (ev.RequiresFlag is null || State.Flags.Contains(ev.RequiresFlag)) &&
        (ev.RequiresNotFlag is null || !State.Flags.Contains(ev.RequiresNotFlag)) &&
        (ev.RequiresItem is null || Inventory_AnyoneHas(ev.RequiresItem));

    private void TriggerEvents(StepResult result, bool entering)
    {
        var map = CurrentMap;
        foreach (var ev in map.EventsAt(State.X, State.Y).ToList())
        {
            if (IsCompleted(map, ev))
            {
                continue;
            }
            if (!RequirementsMet(ev))
            {
                if (ev.FailText is not null)
                {
                    result.Messages.Add(new(ev.FailText, MessageKind.Story));
                }
                continue;
            }
            if (!entering && ev.Type is MapEventKind.Trap or MapEventKind.Encounter or MapEventKind.Teleport)
            {
                continue;
            }
            RunEvent(map, ev, result);
            // A quest step ends processing for this visit so follow-up texts don't stack up.
            if (result.MapChanged || result.CombatStarted || result.Victory || ev.Type == MapEventKind.Quest)
            {
                return;
            }
        }
    }

    private void Complete(GameMap map, MapEventDef ev)
    {
        if (ev.Once)
        {
            State.CompletedEvents.Add(EventKey(map, ev));
        }
        if (ev.SetFlag is not null)
        {
            State.Flags.Add(ev.SetFlag);
        }
    }

    private void Story(StepResult result, MapEventDef ev)
    {
        if (!string.IsNullOrWhiteSpace(ev.Text))
        {
            result.StoryTitle = ev.Name;
            result.StoryText = result.StoryText is null ? ev.Text : result.StoryText + "\n\n" + ev.Text;
            result.Messages.Add(new(ev.Text, MessageKind.Story));
        }
    }

    private void RunEvent(GameMap map, MapEventDef ev, StepResult result)
    {
        switch (ev.Type)
        {
            case MapEventKind.Message:
                Story(result, ev);
                Complete(map, ev);
                break;
            case MapEventKind.Shop or MapEventKind.Training or MapEventKind.Academy when State.IsNight && map.Def.Kind == MapKind.Town:
                result.Messages.Add(new($"{ev.Name ?? "The shop"} is closed for the night. It opens at dawn (5:00). The inn, temple and tavern stay open.", MessageKind.Info));
                break;
            case MapEventKind.Shop:
            case MapEventKind.Temple:
            case MapEventKind.Tavern:
            case MapEventKind.Training:
            case MapEventKind.Academy:
                result.Interaction = ev;
                break;
            case MapEventKind.Inn:
                State.RecallMap = map.Id;
                State.RecallX = ev.X;
                State.RecallY = ev.Y;
                result.Interaction = ev;
                break;
            case MapEventKind.Teleport:
                if (ev.Fare > 0)
                {
                    if (!State.TryPay(ev.Fare))
                    {
                        result.Messages.Add(new(ev.FailText ?? $"Passage costs {ev.Fare} gold, which the party does not have.", MessageKind.Bad));
                        break;
                    }
                    result.Messages.Add(new($"The party pays {ev.Fare} gold for passage.", MessageKind.Info, "coins"));
                }
                Story(result, ev);
                Complete(map, ev);
                var destMap = ev.Map ?? map.Id;
                result.MapChanged = destMap != map.Id || destMap == World.Depths.MapId;
                State.MapId = destMap;
                State.X = ev.ToX;
                State.Y = ev.ToY;
                if (destMap == World.Depths.MapId)
                {
                    EnterDepth(map.Id == World.Depths.MapId ? State.Depth + 1 : 1, result.Messages);
                }
                if (ev.Facing is { } f)
                {
                    State.Facing = f;
                }
                result.Moved = true;
                result.Messages.Add(new("", MessageKind.Info, "door"));
                Explore();
                break;
            case MapEventKind.Treasure:
            case MapEventKind.Quest:
                if (ev.ConsumeItem && ev.RequiresItem is not null)
                {
                    Items.Inventory.RemoveFirst(State.Party, ev.RequiresItem);
                }
                Story(result, ev);
                GrantRewards(ev, result.Messages);
                Complete(map, ev);
                break;
            case MapEventKind.Encounter:
                {
                    Story(result, ev);
                    var monsters = ev.Monsters.SelectMany(fm => CombatEngine.Spawn(Content.Monster(fm.Monster), fm.Count.Roll(Random), Random)).ToList();
                    if (monsters.Count > 0)
                    {
                        StartCombat(monsters, result);
                        _combatEventKey = ev.Once ? EventKey(map, ev) : null;
                        if (ev.SetFlag is not null)
                        {
                            _combatFlag = ev.SetFlag;
                        }
                    }
                    break;
                }
            case MapEventKind.Trap:
                Complete(map, ev);
                RunTrap(map, ev, result);
                break;
            case MapEventKind.Spinner:
                // Classic spinner: no message, the party simply ends up facing a random way.
                State.Facing = (Direction)Random.Next(0, 4);
                if (!string.IsNullOrWhiteSpace(ev.Text))
                {
                    result.Messages.Add(new(ev.Text, MessageKind.Info));
                }
                Complete(map, ev);
                break;
            case MapEventKind.Fountain:
                Story(result, ev);
                foreach (var c in State.Party.Where(c => c.IsAlive))
                {
                    if (ev.Heal)
                    {
                        Rulebook.Heal(c, c.MaxHp);
                    }
                    if (ev.RestoreSp)
                    {
                        c.Sp = c.MaxSp;
                    }
                    c.Conditions &= ~ev.Conditions;
                }
                result.Messages.Add(new("The party feels refreshed.", MessageKind.Good, "heal"));
                Complete(map, ev);
                break;
            case MapEventKind.Riddle:
            case MapEventKind.Choice:
                result.Interaction = ev; // the app asks the question / offers the choice
                break;
            case MapEventKind.Victory:
                Story(result, ev);
                GrantRewards(ev, result.Messages);
                Complete(map, ev);
                State.Won = true;
                result.Victory = true;
                break;
        }
    }

    private string? _combatFlag;
    private HashSet<Characters.Character> _deadBeforeCombat = [];

    private void RunTrap(GameMap map, MapEventDef ev, StepResult result)
    {
        if (State.LevitateSteps > 0 && ev.Trap is TrapEffect.Damage or TrapEffect.Pit or TrapEffect.Teleport)
        {
            result.Messages.Add(new("The party floats over a trap in the floor.", MessageKind.Good));
            return;
        }
        var best = State.Party.Where(c => c.CanAct).OrderByDescending(Rules.Thievery).FirstOrDefault();
        var skill = best is null ? 0 : Rules.Thievery(best);
        if (best is not null && skill > 0 && Random.Chance(skill))
        {
            result.Messages.Add(new($"{best.Name} spots and disarms a trap!", MessageKind.Good, "chest"));
            return;
        }
        result.Messages.Add(new(ev.Text ?? "A trap is sprung!", MessageKind.Bad, "party_hurt"));
        switch (ev.Trap)
        {
            case TrapEffect.Alarm:
                {
                    var monsters = ev.Monsters.SelectMany(fm => CombatEngine.Spawn(Content.Monster(fm.Monster), fm.Count.Roll(Random), Random)).ToList();
                    if (monsters.Count > 0)
                    {
                        StartCombat(monsters, result);
                        _combatEventKey = null;
                    }
                    else
                    {
                        TryRandomEncounter(result, 100);
                    }
                    return;
                }
            case TrapEffect.Teleport:
                TrapTeleport(map, ev, result);
                return;
            case TrapEffect.Pit:
                DamageParty(ev, result);
                TrapTeleport(map, ev, result);
                return;
            default:
                DamageParty(ev, result);
                SleepItOff(result);
                return;
        }
    }

    /// <summary>
    /// Outside combat, sleep wears off: the party loses some time (and may be ambushed while helpless),
    /// then wakes up. Without this a party put entirely to sleep could never act again.
    /// </summary>
    private void SleepItOff(StepResult result)
    {
        if (!State.Party.Any(c => c.IsAlive && c.Has(Condition.Asleep)))
        {
            return;
        }
        PassTime(30, result.Messages);
        TryRandomEncounter(result, Math.Min(60, CurrentMap.Def.EncounterChance * 5));
        if (result.CombatStarted)
        {
            result.Messages.Add(new("Monsters fall upon the sleeping party!", MessageKind.Bad));
            return; // sleepers wake when hit, or when the battle ends
        }
        foreach (var c in State.Party)
        {
            c.Conditions &= ~Condition.Asleep;
        }
        result.Messages.Add(new("Some time later, the party wakes up groggy but unharmed.", MessageKind.Info));
    }

    private void TrapTeleport(GameMap map, MapEventDef ev, StepResult result)
    {
        if (ev.Map is not null || ev.ToX != 0 || ev.ToY != 0)
        {
            var dest = ev.Map ?? map.Id;
            result.MapChanged = dest != map.Id;
            State.MapId = dest;
            State.X = ev.ToX;
            State.Y = ev.ToY;
        }
        else
        {
            // Somewhere random the party could have walked to, avoiding squares with events.
            var options = ReachableNow().Where(p => map.EventsAt(p.X, p.Y).Count == 0 && p != (State.X, State.Y)).ToList();
            if (options.Count > 0)
            {
                (State.X, State.Y) = Random.Pick(options);
            }
        }
        result.Moved = true;
        result.Messages.Add(new("The world lurches - you are somewhere else!", MessageKind.Bad, "spell"));
        Explore();
    }

    /// <summary>Cells the party can currently walk to from where it stands (respects locks and undiscovered secret doors).</summary>
    public IReadOnlyList<(int X, int Y)> ReachableNow()
    {
        var seen = new HashSet<(int, int)> { (State.X, State.Y) };
        var queue = new Queue<(int X, int Y)>();
        queue.Enqueue((State.X, State.Y));
        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            foreach (var d in Enum.GetValues<Direction>())
            {
                var n = (x + d.Dx(), y + d.Dy());
                if (CanPass(x, y, d) && seen.Add(n))
                {
                    queue.Enqueue(n);
                }
            }
        }
        return seen.ToList();
    }

    private void DamageParty(MapEventDef ev, StepResult result)
    {
        foreach (var c in State.Party.Where(c => c.IsAlive))
        {
            var dmg = ev.Damage.Roll(Random);
            if (Rules.SavingThrow(c, 3, Random))
            {
                dmg /= 2;
            }
            else if (ev.Conditions != Condition.None)
            {
                c.Conditions |= ev.Conditions;
            }
            if (dmg > 0)
            {
                result.Messages.Add(new($"{c.Name} takes {dmg} damage.", MessageKind.Bad));
                if (Rules.ApplyDamage(c, dmg))
                {
                    result.Messages.Add(new(c.Has(Condition.Dead) ? $"{c.Name} is killed!" : $"{c.Name} falls unconscious!", MessageKind.Bad));
                }
            }
        }
    }

    private void GrantRewards(MapEventDef ev, List<GameMessage> log)
    {
        var gold = ev.Gold.Roll(Random);
        if (ev.Type == MapEventKind.Treasure)
        {
            State.Count(Chronicle.Keys.Chests);
        }
        if (gold > 0)
        {
            State.Count(Chronicle.Keys.GoldFound, gold);
            State.Gold += gold;
            log.Add(new($"The party finds {gold} gold.", MessageKind.Loot, "coins"));
        }
        if (ev.Gems > 0)
        {
            State.Gems += ev.Gems;
            log.Add(new($"The party finds {ev.Gems} gem{(ev.Gems > 1 ? "s" : "")}.", MessageKind.Loot, "coins"));
        }
        foreach (var itemId in ev.Items)
        {
            GiveItem(new ItemInstance(itemId, Content.Item(itemId).Charges), log);
        }
        if (ev.Xp > 0)
        {
            AwardXp(ev.Xp, log);
        }
    }

    /// <summary>Gives an item to the first party member with room.</summary>
    /// <param name="item">Item.</param>
    /// <param name="log">Messages.</param>
    public bool GiveItem(ItemInstance item, List<GameMessage> log)
    {
        var taker = State.Party.FirstOrDefault(c => c.IsAlive && !c.BackpackFull) ?? State.Party.FirstOrDefault(c => !c.BackpackFull);
        var name = Content.Item(item.ItemId).Name;
        if (taker is null)
        {
            log.Add(new($"No one has room for {name}; it is left behind.", MessageKind.Bad));
            return false;
        }
        taker.Backpack.Add(item);
        log.Add(new($"{taker.Name} takes {name}.", MessageKind.Loot, "chest"));
        return true;
    }

    private void AwardXp(int xp, List<GameMessage> log)
    {
        var eligible = State.Party.Where(c => c.IsAlive && !c.Has(Condition.Unconscious)).ToList();
        foreach (var c in eligible)
        {
            var could = Rules.CanLevelUp(c);
            c.Experience += xp;
            if (!could && Rules.CanLevelUp(c))
            {
                log.Add(new($"{c.Name} is ready to train for level {c.Level + 1}!", MessageKind.Good, "levelup"));
            }
        }
        if (eligible.Count > 0)
        {
            log.Add(new($"Each conscious member gains {xp} experience.", MessageKind.Loot));
        }
    }
}
