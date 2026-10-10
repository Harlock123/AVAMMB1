using AVAMMB1.Core.Content;
using AVAMMB1.Core.Persistence;

namespace AVAMMB1.Core.Session;

/// <summary>When a tip may come up.</summary>
public enum TipMoment
{
    /// <summary>After a step, an action or closing a dialog, while exploring.</summary>
    Exploring,
    /// <summary>When a battle begins.</summary>
    Battle,
    /// <summary>When the party goes into a building (the step's interaction says which).</summary>
    Building,
}

/// <summary>A tip for new players, shown once the first time its situation comes up.</summary>
/// <param name="Id">Id (remembered once seen).</param>
/// <param name="Title">Heading.</param>
/// <param name="Text">Text; <c>{Action}</c> names (see <see cref="InputAction"/>) become that action's key.</param>
/// <param name="Moment">When it may come up.</param>
/// <param name="When">Whether the situation has come up.</param>
public sealed record TipDef(string Id, string Title, string Text, TipMoment Moment, Func<GameSession, StepResult?, bool> When);

/// <summary>The guided first hour: short tips the first time each situation comes up.</summary>
public static class Tips
{
    private static bool Says(StepResult? r, string text) =>
        r is not null && r.Messages.Any(m => m.Text.Contains(text, StringComparison.OrdinalIgnoreCase));

    private static bool Entered(StepResult? r, MapEventKind kind) => r?.Interaction?.Type == kind;

    /// <summary>Every tip, in priority order.</summary>
    public static readonly IReadOnlyList<TipDef> All =
    [
        new("welcome", "Getting around",
            "Walk with {MoveForward} and {MoveBack}, turn with {TurnLeft} and {TurnRight}, side-step with {StrafeLeft} and {StrafeRight}. "
            + "Walk into doors to go in. Archivist Pell, in his study on the west side of town, has work for you. {Help} opens the help.",
            TipMoment.Exploring, (_, _) => true),
        new("battle", "Battle!",
            "Each hero acts in turn: Fight (F) strikes the monster you pick (click it or press 1-9), Cast (C) casts a spell, Block (B) braces. "
            + "Auto (O) fights for you and stops when someone is badly hurt; Run (R) tries to flee. Only the first three heroes in the party "
            + "line can be hit in melee - put archers and casters further back on the character sheet.",
            TipMoment.Battle, (_, _) => true),
        new("shop", "Shopping",
            "Pick an item to see which heroes can use it and how it compares with what they have. Selling pays part of the price; "
            + "items marked as junk sell in one go. Gold is carried by each hero - the purse is the shared part.",
            TipMoment.Building, (_, r) => Entered(r, MapEventKind.Shop)),
        new("inn", "The inn",
            "A night at the inn heals everyone, safe from monsters. The inn also keeps the heroes who aren't travelling with you: "
            + "swap them in and out of the party, or create new ones.",
            TipMoment.Building, (_, r) => Entered(r, MapEventKind.Inn)),
        new("temple", "The temple",
            "Temples cure what rest can't - poison, disease, curses, even death - for a donation. Prayers are cheaper at dawn.",
            TipMoment.Building, (_, r) => Entered(r, MapEventKind.Temple)),
        new("quest", "Your journal",
            "{Journal} opens the journal: every quest you've heard of and what to do next. Its \"Where next\" goal is marked on the "
            + "maps ({Automap}), so you need never be lost.",
            TipMoment.Exploring, (s, _) => QuestJournal.Quests(s.State, s.Content).Count > 0),
        new("dungeon", "Underground",
            "{Automap} shows the map, drawn as you explore; {Note} writes a note on it. {Search} searches for secret doors. "
            + "Dungeons are dark: a lantern, a torch or the Glowlight spell lets the party see further.",
            TipMoment.Exploring, (s, _) => s.CurrentMap.Def.Kind == MapKind.Dungeon),
        new("locked", "Locked doors",
            "A robber in the party tries to pick a lock when you walk into the door - try again if it fails. Some doors need their key, "
            + "found elsewhere, and a few open only when a quest is done.",
            TipMoment.Exploring, (_, r) => Says(r, "locked")),
        new("loot", "Treasure",
            "{Characters} opens the character sheets: equip what you found, pass items to whoever can use them, and read scrolls. "
            + "Shops buy what nobody needs.",
            TipMoment.Exploring, (_, r) => r is not null && r.Messages.Any(m => m.Kind == MessageKind.Loot)),
        new("wounded", "Resting",
            "{Rest} rests for eight hours: everyone heals and casters regain spell points, and each hero eats one food. "
            + "Monsters may interrupt a rest in the wild - the inn is safer. Clerics can heal between fights with {Cast}.",
            TipMoment.Exploring, (s, _) => s.CurrentMap.Def.Kind != MapKind.Town
                && s.State.Party.Any(c => c.IsAlive && c.Hp * 2 < c.MaxHp)),
        new("levelup", "A new level",
            "Experience alone isn't enough to rise a level: visit the training grounds in a town and pay for the training.",
            TipMoment.Exploring, (s, _) => s.State.Party.Any(s.Rules.CanLevelUp)),
        new("night", "Night falls",
            "At night the open country is darker and more dangerous, thieves walk the town streets and most shops close. "
            + "The Night Market in Port Ashkar is the exception.",
            TipMoment.Exploring, (s, _) => s.State.IsNight && s.CurrentMap.Def.Kind != MapKind.Dungeon),
        new("saving", "Saving",
            "{QuickSave} saves and {QuickLoad} loads the quick save. The game also saves by itself on entering a new area and before a "
            + "boss, and {Menu} has the full save and load screens.",
            TipMoment.Exploring, (s, _) => s.State.Steps >= 150),
    ];

    /// <summary>The tip to show now, if any: the first unseen one whose situation has come up (never in a daily challenge).</summary>
    /// <param name="s">The session.</param>
    /// <param name="r">What just happened, if anything.</param>
    /// <param name="moment">The moment.</param>
    /// <param name="seen">Ids of tips already shown.</param>
    public static TipDef? Next(GameSession s, StepResult? r, TipMoment moment, IReadOnlyCollection<string> seen) =>
        !s.IsActive || s.State.DailyChallenge is not null
            ? null
            : All.FirstOrDefault(t => t.Moment == moment && !seen.Contains(t.Id) && t.When(s, r));

    /// <summary>A tip's text with <c>{Action}</c> names replaced by the keys bound to them.</summary>
    /// <param name="text">Tip text.</param>
    /// <param name="bindings">Key bindings.</param>
    public static string WithKeys(string text, IReadOnlyDictionary<InputAction, List<string>> bindings)
    {
        foreach (var action in Enum.GetValues<InputAction>())
        {
            var token = "{" + action + "}";
            if (text.Contains(token, StringComparison.Ordinal))
            {
                var key = bindings.TryGetValue(action, out var keys) && keys.Count > 0 ? KeyName(keys[0]) : "(unbound)";
                text = text.Replace(token, key, StringComparison.Ordinal);
            }
        }
        return text;
    }

    private static string KeyName(string key) => key switch
    {
        "Up" => "↑",
        "Down" => "↓",
        "Left" => "←",
        "Right" => "→",
        "Escape" => "Esc",
        _ => key,
    };
}
