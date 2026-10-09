using AVAMMB1.Core.Rules;

namespace AVAMMB1.Core.Combat;

/// <summary>What the party knows about a monster, for the combat screen.</summary>
public static class MonsterLore
{
    /// <summary>
    /// A one-line description: level, hit points, armor class, undead and resistances for monsters the
    /// party has defeated before; a hint otherwise.
    /// </summary>
    /// <param name="m">The monster.</param>
    /// <param name="known">Whether the party has defeated one of its kind.</param>
    public static string Describe(MonsterInstance m, bool known)
    {
        if (!known)
        {
            return $"{m.Label}: not yet studied - defeat one to learn its strengths and weaknesses.";
        }
        var parts = new List<string> { $"level {m.Def.Level}", $"HP {Math.Max(0, m.Hp)}/{m.MaxHp}", $"AC {m.ArmorClass}" };
        if (m.Def.Undead)
        {
            parts.Add("undead");
        }
        var immune = m.Def.Resistances.Where(r => r.Value >= 100).Select(r => Name(r.Key)).ToList();
        var resists = m.Def.Resistances.Where(r => r.Value is > 0 and < 100).Select(r => $"{Name(r.Key)} {r.Value}%").ToList();
        var weak = m.Def.Resistances.Where(r => r.Value < 0).Select(r => Name(r.Key)).ToList();
        if (immune.Count > 0)
        {
            parts.Add("immune to " + string.Join(", ", immune));
        }
        if (resists.Count > 0)
        {
            parts.Add("resists " + string.Join(", ", resists));
        }
        if (weak.Count > 0)
        {
            parts.Add("vulnerable to " + string.Join(", ", weak));
        }
        return $"{m.Label}: " + string.Join(" · ", parts);
    }

    private static string Name(Element e) => e.ToString().ToLowerInvariant();
}
