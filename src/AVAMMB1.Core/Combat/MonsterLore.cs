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
        parts.AddRange(DefenseParts(m.Def));
        return $"{m.Label}: " + string.Join(" · ", parts);
    }

    private static IEnumerable<string> DefenseParts(Content.MonsterDef d)
    {
        var immune = d.Resistances.Where(r => r.Value >= 100).Select(r => Name(r.Key)).ToList();
        var resists = d.Resistances.Where(r => r.Value is > 0 and < 100).Select(r => $"{Name(r.Key)} {r.Value}%").ToList();
        var weak = d.Resistances.Where(r => r.Value < 0).Select(r => Name(r.Key)).ToList();
        if (immune.Count > 0)
        {
            yield return "immune to " + string.Join(", ", immune);
        }
        if (resists.Count > 0)
        {
            yield return "resists " + string.Join(", ", resists);
        }
        if (weak.Count > 0)
        {
            yield return "vulnerable to " + string.Join(", ", weak);
        }
    }

    /// <summary>Bestiary: "Level 11 · HP 10d8 · AC 9 · speed 14 · 2000 XP · undead".</summary>
    /// <param name="d">Monster.</param>
    public static string Stats(Content.MonsterDef d)
    {
        var parts = new List<string> { $"Level {d.Level}", $"HP {d.HitPoints}", $"AC {d.ArmorClass}", $"speed {d.Speed}", $"{d.Xp} XP" };
        if (d.Boss)
        {
            parts.Add("unique");
        }
        if (d.Undead)
        {
            parts.Add("undead");
        }
        if (d.Regenerates > 0)
        {
            parts.Add($"regenerates {d.Regenerates}");
        }
        return string.Join(" · ", parts);
    }

    /// <summary>Bestiary: how it fights ("claws 2d8 cold, may paralyze; a frost bolt (3d8 cold)").</summary>
    /// <param name="d">Monster.</param>
    public static string Attacks(Content.MonsterDef d)
    {
        var attacks = d.Attacks.Select(a =>
            $"{a.Verb} {a.Damage}" + (a.Element != Element.Physical ? " " + Name(a.Element) : "")
            + (a.Inflicts != Condition.None ? $", may leave you {Name(a.Inflicts)}" : "")
            + (a.Ranged ? " (ranged)" : ""));
        var abilities = d.Abilities.Select(ab =>
        {
            var what = new[]
            {
                ab.Damage.Count > 0 || ab.Damage.Bonus != 0 ? $"{ab.Damage} {Name(ab.Element)}" : null,
                ab.AllTargets ? "the whole party" : null,
                ab.Inflicts != Condition.None ? Name(ab.Inflicts) : null,
                ab.SelfHeal.Count > 0 ? $"heals itself {ab.SelfHeal}" : null,
            }.Where(x => x is not null).ToList();
            return what.Count == 0 ? ab.Name : $"{ab.Name} ({string.Join(", ", what)})";
        });
        return string.Join("; ", attacks.Concat(abilities));
    }

    /// <summary>Bestiary: resistances, or "no special defences".</summary>
    /// <param name="d">Monster.</param>
    public static string Defenses(Content.MonsterDef d)
    {
        var parts = DefenseParts(d).ToList();
        return parts.Count == 0 ? "no special defences" : string.Join(" · ", parts);
    }

    private static string Name(Condition c) => c.ToString().ToLowerInvariant();

    private static string Name(Element e) => e.ToString().ToLowerInvariant();
}
