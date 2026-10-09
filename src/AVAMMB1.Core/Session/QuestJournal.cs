using AVAMMB1.Core.Content;
using AVAMMB1.Core.Items;

namespace AVAMMB1.Core.Session;

/// <summary>A quest as the journal shows it.</summary>
/// <param name="Id">Quest id.</param>
/// <param name="Title">Title.</param>
/// <param name="Main">Whether it is the main quest.</param>
/// <param name="Done">Whether it is complete.</param>
/// <param name="Entries">Journal lines reached so far, oldest first (the last one is the current goal).</param>
public sealed record QuestEntry(string Id, string Title, bool Main, bool Done, IReadOnlyList<string> Entries);

/// <summary>A clue the party has read (a one-time message event).</summary>
/// <param name="Place">Map name.</param>
/// <param name="Name">Event name.</param>
/// <param name="Text">What it said.</param>
public sealed record Discovery(string Place, string Name, string Text);

/// <summary>Builds the journal from the game state; nothing extra is stored in saves.</summary>
public static class QuestJournal
{
    /// <summary>
    /// Quests the party knows about (its first stage is reached, or it is done): main quest first,
    /// then open quests, then completed ones.
    /// </summary>
    /// <param name="state">Game state.</param>
    /// <param name="content">Content.</param>
    public static IReadOnlyList<QuestEntry> Quests(GameState state, ContentDatabase content)
    {
        var list = new List<QuestEntry>();
        foreach (var q in content.Quests)
        {
            var done = q.DoneFlag is not null && state.Flags.Contains(q.DoneFlag);
            if (!done && !Reached(q.Stages[0], state))
            {
                continue; // the party has not heard of this quest yet
            }
            var entries = q.Stages.Where(st => Reached(st, state)).Select(st => st.Text).ToList();
            if (done && q.DoneText.Length > 0)
            {
                entries.Add(q.DoneText);
            }
            if (entries.Count > 0)
            {
                list.Add(new QuestEntry(q.Id, q.Title, q.Main, done, entries));
            }
        }
        return list.OrderBy(e => e.Done).ThenByDescending(e => e.Main).ToList();
    }

    /// <summary>One-time messages (signs, inscriptions, hints) the party has read, by map.</summary>
    /// <param name="state">Game state.</param>
    /// <param name="content">Content.</param>
    public static IReadOnlyList<Discovery> Discoveries(GameState state, ContentDatabase content) =>
        content.Maps.Values.OrderBy(m => m.Def.Name, StringComparer.Ordinal)
            .SelectMany(m => m.AllEvents
                .Where(e => e.Type == MapEventKind.Message && e.Once && e.Id is not null && e.Text is not null && state.CompletedEvents.Contains(e.Id))
                .Select(e => new Discovery(m.Def.Name, e.Name ?? "", e.Text!)))
            .ToList();

    private static bool Reached(QuestStageDef st, GameState state) =>
        st.Flag is not null ? state.Flags.Contains(st.Flag)
        : st.Item is not null ? Inventory.AnyoneHas(state.Party, st.Item)
        : st.Visited is not null && state.Explored.ContainsKey(st.Visited);
}
