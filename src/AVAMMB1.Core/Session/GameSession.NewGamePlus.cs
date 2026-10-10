using AVAMMB1.Core.Characters;
using AVAMMB1.Core.Items;
using AVAMMB1.Core.Rules;

namespace AVAMMB1.Core.Session;

public sealed partial class GameSession
{
    /// <summary>Whether the party can begin New Game+ (the game has been won, and this is not a daily challenge).</summary>
    public bool CanStartNewGamePlus => IsActive && State.Won && State.DailyChallenge is null;

    /// <summary>
    /// Begins the next New Game+ cycle: the same party (levels, gear, spells, gold, hirelings and the heroes at
    /// the inn) starts again at the beginning of a world reset to how it was - quests, treasure, maps and
    /// story - where every monster is stronger and Ascendant treasures can be won. Quest items are left behind.
    /// </summary>
    public List<GameMessage> StartNewGamePlus()
    {
        var log = new List<GameMessage>();
        if (!CanStartNewGamePlus)
        {
            return log;
        }
        var heroes = State.Heroes.ToList();
        State.CyclePartyLevel = heroes.Count == 0 ? 1 : (int)Math.Round(heroes.Average(c => c.Level));
        State.Cycle++;
        State.Won = false;
        State.Flags.Clear();
        State.CompletedEvents.Clear();
        State.Explored.Clear();
        State.FoundSecrets.Clear();
        State.PickedLocks.Clear();
        State.MapNotes.Clear();
        (State.Depth, State.DepthSeed) = (0, 0);
        (State.LightSteps, State.LevitateSteps) = (0, 0);
        var cfg = Content.Config;
        (State.MapId, State.X, State.Y, State.Facing) = (cfg.StartMap, cfg.StartX, cfg.StartY, cfg.StartFacing);
        (State.RecallMap, State.RecallX, State.RecallY) = (cfg.StartMap, cfg.StartX, cfg.StartY);
        var dropped = 0;
        foreach (var c in State.Party.Concat(State.Roster))
        {
            dropped += c.Backpack.RemoveAll(IsQuestItem);
            foreach (var slot in c.Equipment.Where(kv => IsQuestItem(kv.Value)).Select(kv => kv.Key).ToList())
            {
                c.Equipment.Remove(slot);
                dropped++;
            }
        }
        Combat = null;
        Explore();
        log.Add(new($"New Game+ {State.Cycle}: the seasons turn, and the world begins again - stronger. "
            + $"Monsters fight as if {NewGamePlus.LevelBoost(State.Cycle, State.CyclePartyLevel)} levels higher, and Ascendant treasures await.", MessageKind.Story));
        if (dropped > 0)
        {
            log.Add(new($"The quest relics ({dropped}) crumble to dust - their tale must be won again.", MessageKind.Info));
        }
        return log;
    }

    private bool IsQuestItem(ItemInstance i) => Content.Items.TryGetValue(i.ItemId, out var def) && def.Kind == ItemKind.Quest;
}
