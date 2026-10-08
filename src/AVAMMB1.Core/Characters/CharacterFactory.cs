using AVAMMB1.Core.Content;
using AVAMMB1.Core.Dice;
using AVAMMB1.Core.Items;
using AVAMMB1.Core.Rules;

namespace AVAMMB1.Core.Characters;

/// <summary>Rolls and creates new characters.</summary>
/// <param name="rules">The rulebook.</param>
public sealed class CharacterFactory(Rulebook rules)
{
    private static readonly DiceExpression StatDice = new(3, 6, 0);

    /// <summary>Rolls raw attributes (3d6 each) before racial modifiers.</summary>
    /// <param name="rng">Random source.</param>
    public static Dictionary<Stat, int> RollStats(IRandomSource rng) =>
        Enum.GetValues<Stat>().ToDictionary(s => s, _ => StatDice.Roll(rng));

    /// <summary>Applies racial modifiers to raw attributes (clamped to 3..25).</summary>
    /// <param name="raw">Raw attributes.</param>
    /// <param name="race">Race.</param>
    public static Dictionary<Stat, int> ApplyRace(IReadOnlyDictionary<Stat, int> raw, RaceDef race) =>
        Enum.GetValues<Stat>().ToDictionary(
            s => s,
            s => Math.Clamp((raw.TryGetValue(s, out var v) ? v : 10) + (race.StatModifiers.TryGetValue(s, out var m) ? m : 0), 3, 25));

    /// <summary>Whether final attributes and alignment satisfy a class's requirements.</summary>
    /// <param name="stats">Final attributes.</param>
    /// <param name="alignment">Alignment.</param>
    /// <param name="cls">Class.</param>
    public static bool MeetsRequirements(IReadOnlyDictionary<Stat, int> stats, Alignment alignment, ClassDef cls) =>
        cls.Requirements.All(r => stats.TryGetValue(r.Key, out var v) && v >= r.Value) &&
        (cls.AllowedAlignments.Count == 0 || cls.AllowedAlignments.Contains(alignment));

    /// <summary>Classes available for the given final attributes and alignment.</summary>
    /// <param name="stats">Final attributes.</param>
    /// <param name="alignment">Alignment.</param>
    public IEnumerable<ClassDef> EligibleClasses(IReadOnlyDictionary<Stat, int> stats, Alignment alignment) =>
        rules.Content.Classes.Values.Where(c => MeetsRequirements(stats, alignment, c));

    /// <summary>Creates a level 1 character.</summary>
    /// <param name="name">Name.</param>
    /// <param name="raceId">Race id.</param>
    /// <param name="classId">Class id.</param>
    /// <param name="sex">Sex.</param>
    /// <param name="alignment">Alignment.</param>
    /// <param name="finalStats">Attributes with race modifiers applied.</param>
    /// <exception cref="ArgumentException">Thrown when requirements are not met or the name is empty.</exception>
    public Character Create(string name, string raceId, string classId, Sex sex, Alignment alignment, IReadOnlyDictionary<Stat, int> finalStats)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A character needs a name.", nameof(name));
        }
        var db = rules.Content;
        var cls = db.Class(classId);
        _ = db.Race(raceId);
        if (!MeetsRequirements(finalStats, alignment, cls))
        {
            throw new ArgumentException($"{name} does not meet the requirements for {cls.Name}.");
        }
        var c = new Character
        {
            Name = name.Trim().Length > 16 ? name.Trim()[..16] : name.Trim(),
            Race = raceId,
            Class = classId,
            Sex = sex,
            Alignment = alignment,
            Stats = new Dictionary<Stat, int>(finalStats),
            Food = db.Config.StartingFood,
        };
        c.MaxHp = c.Hp = Rulebook.StartingHp(cls, c.BaseStat(Stat.Endurance));
        c.MaxSp = c.Sp = rules.ComputeMaxSp(c);
        var inventory = new Inventory(rules);
        foreach (var itemId in cls.StartingItems)
        {
            var item = new ItemInstance(itemId, db.Item(itemId).Charges);
            c.Backpack.Add(item);
            inventory.Equip(c, c.Backpack.Count - 1);
        }
        return c;
    }

    /// <summary>Creates a character from a premade definition.</summary>
    /// <param name="p">Premade definition.</param>
    public Character CreatePremade(PremadeCharacterDef p) =>
        Create(p.Name, p.Race, p.Class, p.Sex, p.Alignment, Enum.GetValues<Stat>().ToDictionary(s => s, s => p.Stats.TryGetValue(s, out var v) ? v : 12));
}
