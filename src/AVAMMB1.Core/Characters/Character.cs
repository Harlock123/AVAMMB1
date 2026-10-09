using AVAMMB1.Core.Items;
using AVAMMB1.Core.Rules;

namespace AVAMMB1.Core.Characters;

/// <summary>A player character. Pure data; rules live in <see cref="Rulebook"/>.</summary>
public sealed class Character
{
    /// <summary>Maximum number of items carried in the backpack.</summary>
    public const int BackpackSize = 12;

    /// <summary>Unique id.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    /// <summary>Name.</summary>
    public string Name { get; set; } = "";
    /// <summary>Race id.</summary>
    public string Race { get; set; } = "";
    /// <summary>Class id.</summary>
    public string Class { get; set; } = "";
    /// <summary>Sex.</summary>
    public Sex Sex { get; set; }
    /// <summary>Alignment.</summary>
    public Alignment Alignment { get; set; }
    /// <summary>Experience level.</summary>
    public int Level { get; set; } = 1;
    /// <summary>Experience points.</summary>
    public long Experience { get; set; }
    /// <summary>Attribute values (race modifiers already applied).</summary>
    public Dictionary<Stat, int> Stats { get; set; } = new();
    /// <summary>Current hit points.</summary>
    public int Hp { get; set; }
    /// <summary>Maximum hit points.</summary>
    public int MaxHp { get; set; }
    /// <summary>Current spell points.</summary>
    public int Sp { get; set; }
    /// <summary>Maximum spell points.</summary>
    public int MaxSp { get; set; }
    /// <summary>Active conditions.</summary>
    public Condition Conditions { get; set; }
    /// <summary>Gold this character carries personally (separate from the party purse).</summary>
    public int Gold { get; set; }
    /// <summary>Spells learned from tomes.</summary>
    public List<string> LearnedSpells { get; set; } = new();
    /// <summary>Food units carried.</summary>
    public int Food { get; set; }
    /// <summary>Backpack contents.</summary>
    public List<ItemInstance> Backpack { get; set; } = new();
    /// <summary>Equipped items by slot.</summary>
    public Dictionary<EquipSlot, ItemInstance> Equipment { get; set; } = new();

    /// <summary>Base value of an attribute.</summary>
    /// <param name="stat">The attribute.</param>
    public int BaseStat(Stat stat) => Stats.TryGetValue(stat, out var v) ? v : 10;

    /// <summary>True unless dead or stoned.</summary>
    public bool IsAlive => (Conditions & (Condition.Dead | Condition.Stoned)) == 0;

    /// <summary>True when the character can take actions this turn.</summary>
    public bool CanAct => (Conditions & (Condition.Asleep | Condition.Paralyzed | Condition.Unconscious | Condition.Dead | Condition.Stoned)) == 0;

    /// <summary>Whether a given condition is present.</summary>
    /// <param name="c">Condition to test.</param>
    public bool Has(Condition c) => (Conditions & c) != 0;

    /// <summary>The most severe condition, for display.</summary>
    public string StatusText
    {
        get
        {
            Condition[] order =
            [
                Condition.Dead, Condition.Stoned, Condition.Unconscious, Condition.Paralyzed, Condition.Asleep,
                Condition.Poisoned, Condition.Diseased, Condition.Blinded, Condition.Silenced,
            ];
            foreach (var c in order)
            {
                if (Has(c))
                {
                    return c.ToString();
                }
            }
            return "Good";
        }
    }

    /// <summary>Total number of items in the backpack (excluding equipment).</summary>
    public bool BackpackFull => Backpack.Count >= BackpackSize;
}
