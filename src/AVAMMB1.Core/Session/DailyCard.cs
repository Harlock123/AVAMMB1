using System.Globalization;
using System.Text;
using AVAMMB1.Core.Content;
using AVAMMB1.Core.Persistence;

namespace AVAMMB1.Core.Session;

/// <summary>The shareable text summing up a daily challenge run, and the daily records shown on the title screen.</summary>
public static class DailyCard
{
    /// <summary>The result card for a finished daily challenge (plain text, to paste anywhere).</summary>
    /// <param name="state">The run's state.</param>
    /// <param name="content">Game content (class names).</param>
    /// <param name="climbedOut">Whether the party climbed out (rather than falling).</param>
    public static string Text(GameState state, ContentDatabase content, bool climbedOut)
    {
        var inv = CultureInfo.InvariantCulture;
        var deepest = Math.Max(state.DeepestDepth, state.Depth);
        var standing = state.Party.Count(c => c.IsAlive);
        var sb = new StringBuilder();
        sb.Append("AVAM&M Daily Challenge ").Append(state.DailyChallenge ?? "?").Append('\n');
        sb.Append(climbedOut ? $"Climbed out from level {deepest}" : $"Fell on level {state.Depth} (deepest {deepest})").Append('\n');
        sb.Append(new string('▼', Math.Min(deepest, 30))).Append(deepest > 30 ? "+" : "").Append('\n');
        sb.Append(string.Concat(state.Party.Select(c => c.IsAlive ? '♥' : '✝')))
          .Append($" {standing} of {state.Party.Count} standing").Append('\n');
        var kills = state.Kills.Values.Sum();
        var time = TimeSpan.FromSeconds(state.PlaySeconds);
        sb.Append(inv, $"Foes beaten {kills} · battles {state.Stats.GetValueOrDefault(Chronicle.Keys.BattlesWon)} · chests {state.Stats.GetValueOrDefault(Chronicle.Keys.Chests)} · steps {state.Steps}");
        if (state.PlaySeconds > 0)
        {
            sb.Append(inv, $" · {(int)time.TotalHours}:{time.Minutes:00}");
        }
        sb.Append('\n');
        sb.Append(string.Join(", ", state.Party.Select(c => $"{c.Name} ({content.Class(c.Class).Name} {c.Level})")));
        return sb.ToString();
    }

    /// <summary>One line of daily records for the title screen ("Today: level 4 · yesterday: level 9 · best: level 12"), or null if none.</summary>
    /// <param name="hof">The Hall of Fame.</param>
    /// <param name="today">Today's date ("yyyy-MM-dd").</param>
    public static string? Records(HallOfFame hof, string today)
    {
        if (hof.DailyBest.Count == 0)
        {
            return null;
        }
        var parts = new List<string>
        {
            hof.DailyBest.TryGetValue(today, out var t) ? $"Today: level {t}" : "Today: not tried yet",
        };
        if (DateTime.TryParseExact(today, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
            && hof.DailyBest.TryGetValue(day.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), out var y))
        {
            parts.Add($"yesterday: level {y}");
        }
        var best = hof.DailyBest.MaxBy(kv => kv.Value);
        parts.Add($"best: level {best.Value} ({best.Key})");
        return string.Join(" · ", parts);
    }
}
