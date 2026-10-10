using AVAMMB1.Core.Characters;
using AVAMMB1.Core.Combat;
using AVAMMB1.Core.Content;
using AVAMMB1.Core.Dice;
using AVAMMB1.Core.Items;
using AVAMMB1.Core.Magic;
using AVAMMB1.Core.Rules;
using AVAMMB1.Core.World;

namespace AVAMMB1.Core.Session;

/// <summary>Riddles and choices.</summary>
public sealed partial class GameSession
{
    /// <summary>A riddle or choice event the party is standing on and has not settled yet.</summary>
    private (GameMap Map, MapEventDef Ev)? Pending(MapEventDef ev, MapEventKind kind)
    {
        var map = CurrentMap;
        return ev.Type == kind && ev.X == State.X && ev.Y == State.Y && map.AllEvents.Contains(ev) && !IsCompleted(map, ev) && RequirementsMet(ev)
            ? (map, ev) : null;
    }

    /// <summary>"The Map!" -> "map": answers ignore case, punctuation and a leading article.</summary>
    /// <param name="answer">Answer as typed.</param>
    public static string NormalizeAnswer(string answer)
    {
        var s = new string(answer.ToLowerInvariant().Where(ch => char.IsLetterOrDigit(ch) || ch == ' ').ToArray()).Trim();
        foreach (var article in new[] { "a ", "an ", "the " })
        {
            if (s.StartsWith(article, StringComparison.Ordinal))
            {
                s = s[article.Length..].TrimStart();
            }
        }
        return string.Join(' ', s.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>Answers the riddle the party stands before.</summary>
    /// <param name="ev">Riddle event.</param>
    /// <param name="answer">Answer as typed.</param>
    /// <returns>Whether it was right, and what happened.</returns>
    public (bool Correct, List<GameMessage> Messages) AnswerRiddle(MapEventDef ev, string answer)
    {
        var log = new List<GameMessage>();
        if (Pending(ev, MapEventKind.Riddle) is not { } p)
        {
            return (false, log);
        }
        var given = NormalizeAnswer(answer);
        if (given.Length > 0 && ev.Answers.Any(a => NormalizeAnswer(a) == given))
        {
            log.Add(new(ev.SuccessText ?? "That is the answer.", MessageKind.Good, "levelup"));
            GrantRewards(ev, log);
            Complete(p.Map, ev);
            return (true, log);
        }
        log.Add(new(ev.FailText ?? "That is not the answer.", MessageKind.Bad, "bump"));
        if (ev.Penalty.Count > 0 || ev.Penalty.Bonus > 0)
        {
            foreach (var c in State.Party.Where(c => c.IsAlive))
            {
                var dmg = Math.Max(1, ev.Penalty.Roll(Random));
                Rules.ApplyDamage(c, dmg);
                log.Add(new($"{c.Name} takes {dmg} damage.", MessageKind.Bad));
            }
        }
        return (false, log);
    }

    /// <summary>Makes the choice the party stands before.</summary>
    /// <param name="ev">Choice event.</param>
    /// <param name="option">Index into <see cref="MapEventDef.Options"/>.</param>
    public List<GameMessage> Choose(MapEventDef ev, int option)
    {
        var log = new List<GameMessage>();
        if (Pending(ev, MapEventKind.Choice) is not { } p || option < 0 || option >= ev.Options.Count)
        {
            return log;
        }
        var o = ev.Options[option];
        if (o.Text.Length > 0)
        {
            log.Add(new(o.Text, MessageKind.Story));
        }
        foreach (var f in o.SetFlags)
        {
            State.Flags.Add(f);
        }
        GrantRewards(new MapEventDef { Gold = o.Gold, Gems = o.Gems, Items = o.Items, Xp = o.Xp }, log);
        Complete(p.Map, ev);
        return log;
    }
}
