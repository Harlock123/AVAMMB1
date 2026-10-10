using AVAMMB1.Core.Characters;
using AVAMMB1.Core.Content;
using AVAMMB1.Core.Items;
using AVAMMB1.Core.Rules;

namespace AVAMMB1.Core.Session;

public sealed partial class GameSession
{
    /// <summary>The hirelings waiting at a town's inn (not already with the party).</summary>
    /// <param name="townId">The town's map id.</param>
    public IReadOnlyList<HirelingDef> HirelingsAt(string townId) =>
        Content.Config.Hirelings.Where(h => h.Town == townId && !State.Party.Any(c => c.Hireling == h.Id)).ToList();

    /// <summary>A hireling's daily wage (0 for the party's own heroes).</summary>
    /// <param name="c">The character.</param>
    public int WageOf(Character c) =>
        c.Hireling is { } id && Content.Config.Hirelings.FirstOrDefault(h => h.Id == id) is { } def ? def.Wage : 0;

    /// <summary>Where a hireling was hired, for display ("Thornwick"), or null.</summary>
    /// <param name="c">The character.</param>
    public string? HirelingHome(Character c) =>
        c.Hireling is { } id && Content.Config.Hirelings.FirstOrDefault(h => h.Id == id) is { } def && Content.Maps.TryGetValue(def.Town, out var town)
            ? town.Def.Name
            : null;

    /// <summary>Hires an adventurer from the inn of the town the party is in; the first day's wage is paid at once.</summary>
    /// <param name="hirelingId">Hireling id.</param>
    public List<GameMessage> Hire(string hirelingId)
    {
        var log = new List<GameMessage>();
        var def = HirelingsAt(State.MapId).FirstOrDefault(h => h.Id == hirelingId);
        if (def is null || State.DailyChallenge is not null)
        {
            log.Add(new("No one by that name is looking for work here.", MessageKind.Bad));
            return log;
        }
        if (State.Hirelings.Count() >= GameState.MaxHirelings)
        {
            log.Add(new($"The party can take no more than {GameState.MaxHirelings} hirelings.", MessageKind.Bad));
            return log;
        }
        if (!State.TryPay(def.Wage))
        {
            log.Add(new($"{def.Name} wants {def.Wage} gold a day, paid in advance - the party has only {State.Available(null)}.", MessageKind.Bad));
            return log;
        }
        var c = MakeHireling(def);
        State.Party.Add(c);
        State.WagesPaidDay = Math.Max(State.WagesPaidDay, State.Day);
        log.Add(new($"{c.Name} joins the party for {def.Wage} gold a day.", MessageKind.Good, "coins"));
        return log;
    }

    /// <summary>Lets a hireling go (anywhere): they head back to their inn, leaving what they carried in their pack with the party.</summary>
    /// <param name="c">The hireling.</param>
    public List<GameMessage> Dismiss(Character c)
    {
        var log = new List<GameMessage>();
        if (c.Hireling is null || !State.Party.Contains(c))
        {
            return log;
        }
        LetGo(c, log);
        log.Add(new($"{c.Name} wishes the party well and heads back to {HirelingHomeOf(c)}.", MessageKind.Info));
        return log;
    }

    private string HirelingHomeOf(Character c) => HirelingHome(c) ?? "the road";

    private Character MakeHireling(HirelingDef def)
    {
        var c = Factory.Create(def.Name, def.Race, def.Class, def.Sex, def.Alignment, def.Stats);
        c.Hireling = def.Id;
        c.Hair = def.Hair;
        c.Beard = def.Beard;
        c.Gold = 0; // they are paid, not partners
        c.Experience = Rulebook.XpForLevel(Content.Class(c.Class), def.Level);
        // Their own dice: the same hireling always comes with the same hit points, and hiring leaves the game's rolls alone.
        var dice = new Dice.DefaultRandomSource(World.Depths.DailySeed(def.Id, def.Level));
        while (Rules.LevelUp(c, dice) is not null)
        {
        }
        // Seasoned hirelings bring gear to match.
        foreach (var item in c.Equipment.Values.Where(i => Rulebook.Upgradable(Rules.Def(i))))
        {
            item.Plus = Math.Min(4, def.Level / 4);
        }
        c.Hp = c.MaxHp;
        c.Sp = c.MaxSp;
        c.Food = Math.Max(c.Food, Content.Config.StartingFood);
        return c;
    }

    /// <summary>Removes a hireling from the party; anything in their pack goes to the others (what doesn't fit is left behind).</summary>
    private void LetGo(Character c, List<GameMessage> log)
    {
        State.Party.Remove(c);
        foreach (var item in c.Backpack.ToList())
        {
            var taker = State.Party.FirstOrDefault(m => m.Backpack.Count < Character.BackpackSize);
            if (taker is null)
            {
                log.Add(new($"There is no room for {Rules.Def(item).Name}; {c.Name} keeps it.", MessageKind.Bad));
                continue;
            }
            taker.Backpack.Add(item);
            c.Backpack.Remove(item);
            log.Add(new($"{c.Name} hands {Rules.Def(item).Name} to {taker.Name}.", MessageKind.Info));
        }
        State.Gold += c.Gold;
        c.Gold = 0;
    }

    /// <summary>At the start of each day the living hirelings want their wages; those who aren't paid leave.</summary>
    private void PayWages(List<GameMessage> log)
    {
        if (!State.Hirelings.Any())
        {
            State.WagesPaidDay = State.Day;
            return;
        }
        while (State.WagesPaidDay < State.Day)
        {
            State.WagesPaidDay++;
            foreach (var c in State.Hirelings.Where(c => c.IsAlive).ToList())
            {
                var wage = WageOf(c);
                if (State.TryPay(wage))
                {
                    log.Add(new($"{c.Name} is paid {wage} gold for the day.", MessageKind.Info, "coins"));
                    continue;
                }
                LetGo(c, log);
                log.Add(new($"The party can't pay {c.Name}, who leaves for {HirelingHomeOf(c)} in disgust.", MessageKind.Bad));
            }
        }
    }
}
