using AVAMMB1.Core.Characters;
using AVAMMB1.Core.Content;
using AVAMMB1.Core.Dice;
using AVAMMB1.Core.Items;
using AVAMMB1.Core.Rules;

namespace AVAMMB1.Core.Session;

/// <summary>Services offered by town buildings: inn, temple, tavern, training and shops.</summary>
/// <param name="session">The owning session.</param>
public sealed class TownServices(GameSession session)
{
    private GameState State => session.State;

    private static int Price(int basePrice, double factor) => Math.Max(1, (int)Math.Round(basePrice * factor));

    private bool Pay(int cost, List<GameMessage> log, Character? payer = null)
    {
        if (!State.TryPay(cost, payer))
        {
            var who = payer is null ? "The party" : $"{payer.Name} and the purse";
            log.Add(new($"{who} cannot afford {cost} gold (only {State.Available(payer)} available).", MessageKind.Bad));
            return false;
        }
        return true;
    }

    // ---------------- Gold ----------------

    /// <summary>A character puts gold into the party purse.</summary>
    /// <param name="c">Character.</param>
    /// <param name="amount">Amount.</param>
    public List<GameMessage> Deposit(Character c, int amount) =>
        [new($"{c.Name} adds {State.Deposit(c, amount)} gold to the purse.", MessageKind.Info, "coins")];

    /// <summary>A character takes gold from the party purse.</summary>
    /// <param name="c">Character.</param>
    /// <param name="amount">Amount.</param>
    public List<GameMessage> Withdraw(Character c, int amount) =>
        [new($"{c.Name} takes {State.Withdraw(c, amount)} gold from the purse.", MessageKind.Info, "coins")];

    /// <summary>Everyone pools their gold into the purse.</summary>
    public List<GameMessage> PoolAll() =>
        [new($"The party pools {State.PoolAll()} gold. The purse holds {State.Gold}.", MessageKind.Info, "coins")];

    /// <summary>The purse is shared out evenly.</summary>
    public List<GameMessage> ShareEvenly() =>
        [new($"Each member receives {State.ShareEvenly()} gold. {State.Gold} stays in the purse.", MessageKind.Info, "coins")];

    // ---------------- Inn ----------------

    /// <summary>Cost to stay one night at an inn.</summary>
    /// <param name="ev">The inn event.</param>
    public int InnCost(MapEventDef ev) => Price(5, ev.PriceFactor) * Math.Max(1, State.Party.Count(c => c.IsAlive));

    /// <summary>Stays the night: full rest without food or random encounters.</summary>
    /// <param name="ev">The inn event.</param>
    public List<GameMessage> StayAtInn(MapEventDef ev)
    {
        var log = new List<GameMessage>();
        if (!Pay(InnCost(ev), log))
        {
            return log;
        }
        State.Steps += 50;
        foreach (var c in State.Party.Where(c => c.IsAlive))
        {
            session.RestCharacter(c, log, requireFood: false);
        }
        log.Insert(0, new("The party enjoys a warm meal and a soft bed.", MessageKind.Good, "heal"));
        return log;
    }

    /// <summary>Moves a party member to the inn roster.</summary>
    /// <param name="partyIndex">Index in the party.</param>
    /// <param name="takeFromPurse">Gold from the purse the character takes along (on top of their own).</param>
    public List<GameMessage> LeaveAtInn(int partyIndex, int takeFromPurse = 0)
    {
        var log = new List<GameMessage>();
        if (partyIndex < 0 || partyIndex >= State.Party.Count)
        {
            return log;
        }
        if (State.Party.Count == 1)
        {
            log.Add(new("Someone has to stay in the party.", MessageKind.Bad));
            return log;
        }
        if (State.Roster.Count >= GameState.MaxRosterSize)
        {
            log.Add(new("The inn has no more rooms.", MessageKind.Bad));
            return log;
        }
        var c = State.Party[partyIndex];
        var taken = State.Withdraw(c, takeFromPurse);
        State.Party.RemoveAt(partyIndex);
        State.Roster.Add(c);
        log.Add(new(taken > 0
            ? $"{c.Name} takes a room at the inn, keeping {c.Gold} gold ({taken} from the purse)."
            : $"{c.Name} takes a room at the inn" + (c.Gold > 0 ? $", keeping their {c.Gold} gold." : "."), MessageKind.Info));
        return log;
    }

    /// <summary>Moves a roster character into the party.</summary>
    /// <param name="rosterIndex">Index in the roster.</param>
    public List<GameMessage> JoinParty(int rosterIndex)
    {
        var log = new List<GameMessage>();
        if (rosterIndex < 0 || rosterIndex >= State.Roster.Count)
        {
            return log;
        }
        if (State.Party.Count >= GameState.MaxPartySize)
        {
            log.Add(new("The party is already full.", MessageKind.Bad));
            return log;
        }
        var c = State.Roster[rosterIndex];
        State.Roster.RemoveAt(rosterIndex);
        State.Party.Add(c);
        log.Add(new($"{c.Name} joins the party.", MessageKind.Good));
        return log;
    }

    /// <summary>Adds a freshly created character to the roster (or party if there is room).</summary>
    /// <param name="c">New character.</param>
    public List<GameMessage> Recruit(Character c)
    {
        var log = new List<GameMessage>();
        if (State.Party.Count < GameState.MaxPartySize)
        {
            State.Party.Add(c);
            log.Add(new($"{c.Name} joins the party.", MessageKind.Good));
        }
        else if (State.Roster.Count < GameState.MaxRosterSize)
        {
            State.Roster.Add(c);
            log.Add(new($"{c.Name} waits at the inn.", MessageKind.Info));
        }
        else
        {
            log.Add(new("There is no room for another adventurer.", MessageKind.Bad));
        }
        return log;
    }

    /// <summary>Moves a party member one place up the marching order.</summary>
    /// <param name="partyIndex">Index to move.</param>
    public void MoveUp(int partyIndex)
    {
        if (partyIndex > 0 && partyIndex < State.Party.Count)
        {
            (State.Party[partyIndex - 1], State.Party[partyIndex]) = (State.Party[partyIndex], State.Party[partyIndex - 1]);
        }
    }

    // ---------------- Temple ----------------

    /// <summary>Price for the temple to fully restore a character, or 0 if nothing is needed.</summary>
    /// <param name="c">Character.</param>
    /// <param name="ev">Temple event (price factor).</param>
    public int TempleCost(Character c, MapEventDef ev)
    {
        var cost = 0;
        if (c.Has(Condition.Dead))
        {
            cost += 150 * c.Level;
        }
        if (c.Has(Condition.Stoned))
        {
            cost += 200 * c.Level;
        }
        if (c.Has(Condition.Paralyzed) || c.Has(Condition.Diseased) || c.Has(Condition.Poisoned) || c.Has(Condition.Blinded) || c.Has(Condition.Silenced))
        {
            cost += 40 * c.Level;
        }
        if (c.Hp < c.MaxHp || c.Has(Condition.Unconscious))
        {
            cost += 5 + 2 * c.Level;
        }
        return cost == 0 ? 0 : Price(cost, ev.PriceFactor);
    }

    /// <summary>Heals, cures and resurrects a character for gold.</summary>
    /// <param name="c">Character.</param>
    /// <param name="ev">Temple event.</param>
    public List<GameMessage> TempleHeal(Character c, MapEventDef ev)
    {
        var log = new List<GameMessage>();
        var cost = TempleCost(c, ev);
        if (cost == 0)
        {
            log.Add(new($"{c.Name} needs no healing.", MessageKind.Info));
            return log;
        }
        if (!Pay(cost, log, c))
        {
            return log;
        }
        var wasDead = c.Has(Condition.Dead);
        c.Conditions = Condition.None;
        c.Hp = c.MaxHp;
        if (wasDead)
        {
            c.Stats[Stat.Endurance] = Math.Max(3, c.BaseStat(Stat.Endurance) - 1);
        }
        log.Add(new($"The priests tend to {c.Name}. ({cost} gold)", MessageKind.Good, "heal"));
        return log;
    }

    /// <summary>Donates gold to the temple for a small luck blessing on the party leader.</summary>
    /// <param name="ev">Temple event.</param>
    public List<GameMessage> Donate(MapEventDef ev)
    {
        var log = new List<GameMessage>();
        var cost = Price(100, ev.PriceFactor);
        if (!Pay(cost, log))
        {
            return log;
        }
        if (session.Random.Chance(25) && State.Party.FirstOrDefault(c => c.IsAlive) is { } lucky && lucky.BaseStat(Stat.Luck) < 20)
        {
            lucky.Stats[Stat.Luck] = lucky.BaseStat(Stat.Luck) + 1;
            log.Add(new($"A warm glow surrounds {lucky.Name}. Luck +1!", MessageKind.Good, "levelup"));
        }
        else
        {
            log.Add(new("The priests thank you for your generosity.", MessageKind.Info, "coins"));
        }
        return log;
    }

    // ---------------- Tavern ----------------

    /// <summary>Cost to fill everyone's packs with food.</summary>
    /// <param name="ev">Tavern event.</param>
    public int FoodCost(MapEventDef ev)
    {
        var max = session.Content.Config.MaxFood;
        var units = State.Party.Where(c => c.IsAlive).Sum(c => Math.Max(0, max - c.Food));
        return units == 0 ? 0 : Price(units, ev.PriceFactor);
    }

    /// <summary>Buys food until every member carries the maximum.</summary>
    /// <param name="ev">Tavern event.</param>
    public List<GameMessage> BuyFood(MapEventDef ev)
    {
        var log = new List<GameMessage>();
        var cost = FoodCost(ev);
        if (cost == 0)
        {
            log.Add(new("Everyone's packs are already full of food.", MessageKind.Info));
            return log;
        }
        if (!Pay(cost, log))
        {
            return log;
        }
        foreach (var c in State.Party.Where(c => c.IsAlive))
        {
            c.Food = session.Content.Config.MaxFood;
        }
        log.Add(new($"The party stocks up on provisions for {cost} gold.", MessageKind.Good, "coins"));
        return log;
    }

    /// <summary>Buys a round of drinks and hears a rumor.</summary>
    /// <param name="ev">Tavern event.</param>
    public List<GameMessage> BuyDrinks(MapEventDef ev)
    {
        var log = new List<GameMessage>();
        if (!Pay(Price(2, ev.PriceFactor) * Math.Max(1, State.Party.Count), log))
        {
            return log;
        }
        var rumor = ev.Rumors.Count == 0 ? "The barkeep has nothing interesting to say." : session.Random.Pick(ev.Rumors);
        log.Add(new($"Over a round of ale you hear: \"{rumor}\"", MessageKind.Story, "drink"));
        return log;
    }

    // ---------------- Training ----------------

    /// <summary>Trains a character to the next level.</summary>
    /// <param name="c">Character.</param>
    /// <param name="ev">Training event (price factor).</param>
    public List<GameMessage> Train(Character c, MapEventDef ev)
    {
        var log = new List<GameMessage>();
        if (!session.Rules.CanLevelUp(c))
        {
            var need = session.Rules.XpForNextLevel(c) - c.Experience;
            log.Add(new(c.IsAlive ? $"{c.Name} needs {need} more experience." : $"{c.Name} is in no state to train.", MessageKind.Info));
            return log;
        }
        if (!Pay(TrainingCost(c, ev), log, c))
        {
            return log;
        }
        var result = session.Rules.LevelUp(c, session.Random)!;
        log.Add(new($"{c.Name} reaches level {result.NewLevel}! (+{result.HpGained} HP)", MessageKind.Good, "levelup"));
        if (result.NewSpellLevel > 0)
        {
            log.Add(new($"{c.Name} can now cast level {result.NewSpellLevel} spells.", MessageKind.Good));
        }
        return log;
    }

    /// <summary>Training price for a character.</summary>
    /// <param name="c">Character.</param>
    /// <param name="ev">Training event.</param>
    public static int TrainingCost(Character c, MapEventDef ev) => Price(Rulebook.TrainingCost(c), ev.PriceFactor);

    // ---------------- Shops ----------------

    /// <summary>Buy price for an item in a shop.</summary>
    /// <param name="shop">Shop.</param>
    /// <param name="item">Item.</param>
    public static int BuyPrice(ShopDef shop, ItemDef item) => Price(item.Price, shop.PriceFactor);

    /// <summary>Buys an item for a character.</summary>
    /// <param name="shop">Shop.</param>
    /// <param name="itemId">Item id.</param>
    /// <param name="buyer">Receiving character.</param>
    public List<GameMessage> Buy(ShopDef shop, string itemId, Character buyer)
    {
        var log = new List<GameMessage>();
        var item = session.Content.Item(itemId);
        if (!shop.Stock.Contains(itemId))
        {
            log.Add(new("That is not for sale here.", MessageKind.Bad));
            return log;
        }
        if (buyer.BackpackFull)
        {
            log.Add(new($"{buyer.Name}'s pack is full.", MessageKind.Bad));
            return log;
        }
        if (!Pay(BuyPrice(shop, item), log, buyer))
        {
            return log;
        }
        buyer.Backpack.Add(new ItemInstance(itemId, item.Charges));
        var warn = item.Slot is not null && !Rulebook.CanUse(buyer, item) ? $" (a {session.Content.Class(buyer.Class).Name} cannot use it)" : "";
        log.Add(new($"{buyer.Name} buys {item.Name}{warn}.", MessageKind.Good, "coins"));
        return log;
    }

    /// <summary>Sells an item from a character's backpack.</summary>
    /// <param name="seller">Character.</param>
    /// <param name="backpackIndex">Backpack index.</param>
    public List<GameMessage> Sell(Character seller, int backpackIndex)
    {
        var log = new List<GameMessage>();
        if (backpackIndex < 0 || backpackIndex >= seller.Backpack.Count)
        {
            return log;
        }
        var item = session.Rules.Def(seller.Backpack[backpackIndex]);
        if (item.Kind == ItemKind.Quest)
        {
            log.Add(new($"The merchant won't touch {item.Name}.", MessageKind.Bad));
            return log;
        }
        var price = Rulebook.SellPrice(item);
        seller.Backpack.RemoveAt(backpackIndex);
        State.Gold += price;
        log.Add(new($"{seller.Name} sells {item.Name} for {price} gold.", MessageKind.Good, "coins"));
        return log;
    }
}
