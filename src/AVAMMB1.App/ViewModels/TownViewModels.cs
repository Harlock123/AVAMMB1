using System.Collections.ObjectModel;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using AVAMMB1.Core.Characters;
using AVAMMB1.Core.Content;
using AVAMMB1.Core.Items;
using AVAMMB1.Core.Rules;
using AVAMMB1.Core.Session;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AVAMMB1.App.ViewModels;

/// <summary>Modal story / message dialog.</summary>
public sealed partial class StoryViewModel : ViewModelBase
{
    private readonly GameViewModel _game;
    private readonly Action? _then;

    /// <summary>Creates the dialog.</summary>
    /// <param name="game">Owner.</param>
    /// <param name="title">Title.</param>
    /// <param name="text">Body.</param>
    /// <param name="then">Action after closing (default: close overlay).</param>
    public StoryViewModel(GameViewModel game, string title, string text, Action? then)
    {
        _game = game;
        Title = title;
        Text = text;
        _then = then;
        game.Services.Audio.PlaySfx("book");
    }

    /// <summary>Title.</summary>
    public string Title { get; }
    /// <summary>Body.</summary>
    public string Text { get; }

    /// <summary>Dismisses the dialog.</summary>
    [RelayCommand]
    private void Ok()
    {
        if (_then is null)
        {
            _game.CloseOverlay();
        }
        else
        {
            _then();
        }
    }

    /// <inheritdoc />
    public override bool HandleKey(Key key)
    {
        if (key is Key.Enter or Key.Space or Key.Escape)
        {
            Ok();
            return true;
        }
        return false;
    }
}

/// <summary>Common base for building dialogs.</summary>
public abstract partial class BuildingViewModel : ViewModelBase
{
    /// <summary>Creates the dialog.</summary>
    /// <param name="game">Owner.</param>
    /// <param name="title">Building name.</param>
    protected BuildingViewModel(GameViewModel game, string title)
    {
        Game = game;
        Title = title;
        Members = game.Party;
    }

    /// <summary>Owner.</summary>
    protected GameViewModel Game { get; }

    /// <summary>Session.</summary>
    protected GameSession Session => Game.Services.Session;

    /// <summary>Building name.</summary>
    public string Title { get; }

    /// <summary>Party members.</summary>
    public ObservableCollection<PartyMemberViewModel> Members { get; }

    /// <summary>Gold display.</summary>
    public string GoldText => $"Party purse: {Session.State.Gold} gold   (everyone's gold: {Session.State.TotalGold})";

    /// <summary>Last action feedback.</summary>
    [ObservableProperty]
    private string _feedback = "";

    /// <summary>Reports the result of an action.</summary>
    /// <param name="log">Messages.</param>
    protected void Report(List<GameMessage> log)
    {
        Game.AddMessages(log);
        Feedback = string.Join("\n", log.Where(m => m.Text.Length > 0).Select(m => m.Text));
        foreach (var m in Game.Party)
        {
            m.Refresh();
        }
        OnPropertyChanged(nameof(GoldText));
        RefreshRows();
    }

    /// <summary>Refreshes derived rows after an action.</summary>
    protected virtual void RefreshRows()
    {
    }

    /// <summary>Leaves the building.</summary>
    [RelayCommand]
    private void Leave() => Game.CloseOverlay();
}

/// <summary>A per-character service row (temple, training).</summary>
public sealed partial class ServiceRowViewModel : ObservableObject
{
    /// <summary>Creates the row.</summary>
    /// <param name="member">Member.</param>
    public ServiceRowViewModel(PartyMemberViewModel member)
    {
        Member = member;
    }

    /// <summary>Member.</summary>
    public PartyMemberViewModel Member { get; }

    /// <summary>Price / description.</summary>
    [ObservableProperty]
    private string _info = "";

    /// <summary>Whether the service is available.</summary>
    [ObservableProperty]
    private bool _available;
}

/// <summary>Temple: heal, cure, resurrect, donate.</summary>
public sealed partial class TempleViewModel : BuildingViewModel
{
    private readonly MapEventDef _ev;

    /// <summary>Creates the dialog.</summary>
    /// <param name="game">Owner.</param>
    /// <param name="ev">Temple event.</param>
    public TempleViewModel(GameViewModel game, MapEventDef ev) : base(game, ev.Name ?? "Temple")
    {
        _ev = ev;
        Rows = new ObservableCollection<ServiceRowViewModel>(game.Party.Select(m => new ServiceRowViewModel(m)));
        RefreshRows();
        Feedback = Session.Town.DawnPrayers
            ? "Dawn prayers: the priests ask a quarter less for healing until eight o'clock."
            : "Priests in white robes offer healing - for a donation. (At dawn, from five to eight, they ask a quarter less.)";
    }

    /// <summary>Rows.</summary>
    public ObservableCollection<ServiceRowViewModel> Rows { get; }

    /// <inheritdoc />
    protected override void RefreshRows()
    {
        foreach (var r in Rows)
        {
            var cost = Session.Town.TempleCost(r.Member.Character, _ev);
            r.Info = cost == 0 ? "In good health" : $"{r.Member.Status}: heal for {cost} gold";
            r.Available = cost > 0;
        }
    }

    [RelayCommand]
    private void Heal(ServiceRowViewModel row) => Report(Session.Town.TempleHeal(row.Member.Character, _ev));

    [RelayCommand]
    private void Donate() => Report(Session.Town.Donate(_ev));
}

/// <summary>Training grounds: level up.</summary>
public sealed partial class TrainingViewModel : BuildingViewModel
{
    private readonly MapEventDef _ev;

    /// <summary>Creates the dialog.</summary>
    /// <param name="game">Owner.</param>
    /// <param name="ev">Training event.</param>
    public TrainingViewModel(GameViewModel game, MapEventDef ev) : base(game, ev.Name ?? "Training Grounds")
    {
        _ev = ev;
        Rows = new ObservableCollection<ServiceRowViewModel>(game.Party.Select(m => new ServiceRowViewModel(m)));
        RefreshRows();
        Feedback = "\"Experience is earned out there. In here, we turn it into skill.\"";
    }

    /// <summary>Rows.</summary>
    public ObservableCollection<ServiceRowViewModel> Rows { get; }

    /// <inheritdoc />
    protected override void RefreshRows()
    {
        foreach (var r in Rows)
        {
            var c = r.Member.Character;
            var rules = Session.Rules;
            if (rules.CanLevelUp(c))
            {
                r.Info = $"Ready for level {c.Level + 1}: {AVAMMB1.Core.Session.TownServices.TrainingCost(c, _ev)} gold";
                r.Available = true;
            }
            else
            {
                r.Info = c.IsAlive ? $"Level {c.Level}: needs {rules.XpForNextLevel(c) - c.Experience} more XP" : "Cannot train";
                r.Available = false;
            }
        }
    }

    [RelayCommand]
    private void Train(ServiceRowViewModel row) => Report(Session.Town.Train(row.Member.Character, _ev));
}

/// <summary>Academy: buy permanent statistic points (an expensive late-game gold sink).</summary>
public sealed partial class AcademyViewModel : BuildingViewModel
{
    private readonly MapEventDef _ev;

    /// <summary>Creates the dialog.</summary>
    /// <param name="game">Owner.</param>
    /// <param name="ev">Academy event.</param>
    public AcademyViewModel(GameViewModel game, MapEventDef ev) : base(game, ev.Name ?? "Academy")
    {
        _ev = ev;
        Rows = new ObservableCollection<ServiceRowViewModel>(game.Party.Select(m => new ServiceRowViewModel(m)));
        _selected = Rows.FirstOrDefault();
        RefreshRows();
        Feedback = $"\"Talent is a gift; mastery is bought - one lesson at a time.\" Each character can learn up to {AVAMMB1.Core.Session.TownServices.AcademyMaxPoints} points, and every lesson costs more than the last.";
    }

    /// <summary>Rows.</summary>
    public ObservableCollection<ServiceRowViewModel> Rows { get; }

    /// <summary>The student.</summary>
    [ObservableProperty]
    private ServiceRowViewModel? _selected;

    partial void OnSelectedChanged(ServiceRowViewModel? value) => RefreshRows();

    /// <summary>Statistic buttons for the selected student.</summary>
    public IReadOnlyList<StatLesson> Lessons => Selected is { } r
        ? Enum.GetValues<Stat>().Select(st => new StatLesson(st, r.Member.Character.BaseStat(st), AVAMMB1.Core.Session.TownServices.AcademyBlock(r.Member.Character, st) is null)).ToList()
        : [];

    /// <inheritdoc />
    protected override void RefreshRows()
    {
        foreach (var r in Rows)
        {
            var c = r.Member.Character;
            r.Available = c.IsAlive && c.AcademyPoints < AVAMMB1.Core.Session.TownServices.AcademyMaxPoints;
            r.Info = r.Available
                ? $"{c.AcademyPoints}/{AVAMMB1.Core.Session.TownServices.AcademyMaxPoints} points learned - next lesson {AVAMMB1.Core.Session.TownServices.AcademyCost(c, _ev)} gold"
                : c.IsAlive ? "Has learned all the academy can teach" : "Cannot study";
        }
        OnPropertyChanged(nameof(Lessons));
    }

    [RelayCommand]
    private void Choose(ServiceRowViewModel row) => Selected = row;

    [RelayCommand]
    private void Study(StatLesson lesson)
    {
        if (Selected is { } r)
        {
            Report(Session.Town.Study(r.Member.Character, lesson.Stat, _ev));
        }
    }
}

/// <summary>A statistic that can be studied.</summary>
/// <param name="Stat">Statistic.</param>
/// <param name="Value">Current value.</param>
/// <param name="Enabled">Whether it can be raised now.</param>
public sealed record StatLesson(Stat Stat, int Value, bool Enabled)
{
    /// <summary>Button label.</summary>
    public string Label => $"{Stat} {Value} +1";
}

/// <summary>Tavern: food, drinks and rumors.</summary>
public sealed partial class TavernViewModel : BuildingViewModel
{
    private readonly MapEventDef _ev;

    /// <summary>Creates the dialog.</summary>
    /// <param name="game">Owner.</param>
    /// <param name="ev">Tavern event.</param>
    public TavernViewModel(GameViewModel game, MapEventDef ev) : base(game, ev.Name ?? "Tavern")
    {
        _ev = ev;
        Feedback = "The room is warm, loud and smells of stew.";
    }

    /// <summary>Food price label.</summary>
    public string FoodLabel => Session.Town.FoodCost(_ev) is var c && c > 0 ? $"Stock up on food ({c} gold)" : "Packs are full of food";

    /// <summary>Food carried by each member.</summary>
    public string FoodStatus => string.Join("   ", Session.State.Party.Select(c => $"{c.Name}: {c.Food}"));

    [RelayCommand]
    private void BuyFood() => Report(Session.Town.BuyFood(_ev));

    [RelayCommand]
    private void BuyDrinks() => Report(Session.Town.BuyDrinks(_ev));

    /// <inheritdoc />
    protected override void RefreshRows()
    {
        OnPropertyChanged(nameof(FoodLabel));
        OnPropertyChanged(nameof(FoodStatus));
    }
}

/// <summary>Inn: rest, save, and manage the roster.</summary>
public sealed partial class InnViewModel : BuildingViewModel
{
    private readonly MapEventDef _ev;

    /// <summary>Creates the dialog.</summary>
    /// <param name="game">Owner.</param>
    /// <param name="ev">Inn event.</param>
    public InnViewModel(GameViewModel game, MapEventDef ev) : base(game, ev.Name ?? "Inn")
    {
        _ev = ev;
        Feedback = "The innkeeper waves you in. (The Homeward spell will now return you here.)";
        RefreshRows();
    }

    /// <summary>Characters waiting at the inn.</summary>
    public ObservableCollection<Character> Roster { get; } = new();

    /// <summary>Room price label.</summary>
    public string StayLabel => $"Stay the night ({Session.Town.InnCost(_ev)} gold)";

    [RelayCommand]
    private void Stay() => Report(Session.Town.StayAtInn(_ev));

    [RelayCommand]
    private void SaveGame() => Game.Overlay = new SaveLoadViewModel(Game.Main, saving: true, onClose: () => Game.Overlay = this);

    /// <summary>Member about to stay behind (asks how much purse gold to take).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsChoosingGold), nameof(LeavePrompt))]
    private PartyMemberViewModel? _leaving;

    /// <summary>Gold from the purse the leaving member takes along.</summary>
    [ObservableProperty]
    private decimal? _takeGold = 0;

    /// <summary>Whether the take-gold prompt is showing.</summary>
    public bool IsChoosingGold => Leaving is not null;

    /// <summary>Purse size, for the take-gold prompt.</summary>
    public int PurseGold => Session.State.Gold;

    /// <summary>Prompt text.</summary>
    public string LeavePrompt => Leaving is null ? "" :
        $"{Leaving.Name} keeps their own {Leaving.Character.Gold} gold. How much from the purse ({Session.State.Gold}) do they take along?";

    /// <summary>Adventurers for hire at this inn.</summary>
    public ObservableCollection<HireRow> ForHire { get; } = new();

    /// <summary>Whether to show the hiring section (someone for hire here, or hirelings along).</summary>
    public bool HasForHire => ForHire.Count > 0 || Session.State.Hirelings.Any();

    /// <summary>Shown when no one (else) is for hire here.</summary>
    public bool NoOneForHire => ForHire.Count == 0;

    /// <summary>A note on hirelings ("2 of 2 hired" and so on).</summary>
    public string HireNote => $"Hirelings travel with your six heroes for a daily wage and take no share of experience. With the party: {Session.State.Hirelings.Count()} of {GameState.MaxHirelings}.";

    [RelayCommand]
    private void Hire(HireRow row)
    {
        Report(Session.Hire(row.Id));
        Game.Refresh();
        RefreshRows();
    }

    [RelayCommand]
    private void LeaveMember(PartyMemberViewModel m)
    {
        if (m.IsHireling)
        {
            Report(Session.Dismiss(m.Character));
            Game.Refresh();
            RefreshRows();
            return;
        }
        Editing = null;
        TakeGold = 0;
        Leaving = m;
        OnPropertyChanged(nameof(PurseGold));
    }

    [RelayCommand]
    private void ConfirmLeave()
    {
        if (Leaving is { } m)
        {
            Leaving = null;
            Report(Session.Town.LeaveAtInn(m.Index, (int)Math.Clamp(TakeGold ?? 0, 0, Session.State.Gold)));
            Game.Refresh();
        }
    }

    [RelayCommand]
    private void CancelLeave() => Leaving = null;

    [RelayCommand]
    private void MoveUp(PartyMemberViewModel m)
    {
        Session.Town.MoveUp(m.Index);
        Game.Refresh();
    }

    [RelayCommand]
    private void Join(Character c)
    {
        Report(Session.Town.JoinParty(Session.State.Roster.IndexOf(c)));
        Game.Refresh();
    }

    [RelayCommand]
    private void Recruit() => Game.Main.ShowRecruit();

    /// <summary>Member whose name and looks are being changed.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditing), nameof(EditPortrait))]
    private PartyMemberViewModel? _editing;

    /// <summary>Name being typed.</summary>
    [ObservableProperty]
    private string _editName = "";

    /// <summary>Hairstyle being chosen.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EditPortrait), nameof(EditHairLabel))]
    private string? _editHair;

    /// <summary>Beard being chosen.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EditPortrait), nameof(EditBeardLabel))]
    private string? _editBeard;

    /// <summary>Whether the name-and-looks panel is open.</summary>
    public bool IsEditing => Editing is not null;

    /// <summary>Preview of the new look.</summary>
    public Avalonia.Media.Imaging.Bitmap? EditPortrait => Editing?.Character is { } c
        ? Game.Services.Portrait(c.Race, c.Sex, c.Class, EditHair, EditBeard) : null;

    /// <summary>Hair choice shown.</summary>
    public string EditHairLabel => EditHair is null ? "class headgear" : PortraitStyles.Describe(EditHair);

    /// <summary>Beard choice shown.</summary>
    public string EditBeardLabel => EditBeard is null ? "none" : PortraitStyles.Describe(EditBeard);

    [RelayCommand]
    private void Edit(PartyMemberViewModel m)
    {
        Leaving = null;
        Editing = m;
        EditName = m.Character.Name;
        EditHair = m.Character.Hair;
        EditBeard = m.Character.Beard;
    }

    [RelayCommand]
    private void RandomEditName()
    {
        if (Editing?.Character is { } c)
        {
            EditName = NameGenerator.Generate(c.Race, c.Sex, Session.Random);
        }
    }

    [RelayCommand]
    private void NextEditHair() => EditHair = PortraitStyles.Cycle(PortraitStyles.Hair, EditHair, 1);

    [RelayCommand]
    private void PreviousEditHair() => EditHair = PortraitStyles.Cycle(PortraitStyles.Hair, EditHair, -1);

    [RelayCommand]
    private void NextEditBeard() => EditBeard = PortraitStyles.Cycle(PortraitStyles.Beards, EditBeard, 1);

    [RelayCommand]
    private void PreviousEditBeard() => EditBeard = PortraitStyles.Cycle(PortraitStyles.Beards, EditBeard, -1);

    [RelayCommand]
    private void SaveEdit()
    {
        if (Editing?.Character is not { } c)
        {
            return;
        }
        var log = Session.Town.Rename(c, EditName);
        if (log.All(m => m.Kind != MessageKind.Bad))
        {
            c.Hair = EditHair;
            c.Beard = EditBeard;
            Editing = null;
        }
        Report(log);
        Game.Refresh();
    }

    [RelayCommand]
    private void CancelEdit() => Editing = null;

    /// <inheritdoc />
    protected override void RefreshRows()
    {
        Roster.Clear();
        foreach (var c in Session.State.Roster)
        {
            Roster.Add(c);
        }
        ForHire.Clear();
        foreach (var h in Session.HirelingsAt(Session.State.MapId))
        {
            ForHire.Add(new HireRow(h.Id, h.Name,
                $"L{h.Level} {Game.Services.Content.Race(h.Race).Name} {Game.Services.Content.Class(h.Class).Name} - {h.Wage} gold a day", h.Blurb));
        }
        OnPropertyChanged(nameof(StayLabel));
        OnPropertyChanged(nameof(HasForHire));
        OnPropertyChanged(nameof(NoOneForHire));
        OnPropertyChanged(nameof(HireNote));
    }
}

/// <summary>An adventurer for hire at an inn.</summary>
/// <param name="Id">Hireling id.</param>
/// <param name="Name">Name.</param>
/// <param name="Terms">Level, class and wage.</param>
/// <param name="Blurb">A line about them.</param>
public sealed record HireRow(string Id, string Name, string Terms, string Blurb);

/// <summary>An item offered or owned in a shop.</summary>
public sealed class ShopItemViewModel
{
    /// <summary>Creates the row.</summary>
    /// <param name="def">Item.</param>
    /// <param name="price">Price shown.</param>
    /// <param name="icon">Icon.</param>
    /// <param name="usable">Whether the selected character can use it.</param>
    /// <param name="index">Backpack index (sell rows).</param>
    /// <param name="compare">How it compares with what the shopper wears ("" for non-gear).</param>
    /// <param name="upgrade">Whether it would be an upgrade for the shopper.</param>
    /// <param name="junk">Whether nobody in the party needs it (sell rows).</param>
    /// <param name="name">Name to show (e.g. "Long Sword +2"), if not the item's own.</param>
    public ShopItemViewModel(ItemDef def, int price, Bitmap? icon, bool usable, int index = -1, string compare = "", bool upgrade = false, bool junk = false, string? name = null)
    {
        Def = def;
        _name = name;
        Price = price;
        Icon = icon;
        Usable = usable;
        Index = index;
        Compare = compare;
        IsUpgrade = upgrade;
        IsJunk = junk;
    }

    /// <summary>Comparison with the shopper's gear.</summary>
    public string Compare { get; }
    /// <summary>Whether there is a comparison to show.</summary>
    public bool HasCompare => Compare.Length > 0;
    /// <summary>Better than what the shopper has.</summary>
    public bool IsUpgrade { get; }
    /// <summary>Nobody in the party needs it.</summary>
    public bool IsJunk { get; }

    /// <summary>Item.</summary>
    public ItemDef Def { get; }
    private readonly string? _name;
    /// <summary>Name.</summary>
    public string Name => _name ?? Def.Name;
    /// <summary>Price.</summary>
    public int Price { get; }
    /// <summary>Icon.</summary>
    public Bitmap? Icon { get; }
    /// <summary>Usable by selected member.</summary>
    public bool Usable { get; }
    /// <summary>Backpack index for selling.</summary>
    public int Index { get; }
    /// <summary>Stats summary.</summary>
    public string Summary => ItemText.Describe(Def) + (Usable ? "" : "  (cannot use)");
}

/// <summary>A piece of gear the smith can improve.</summary>
/// <param name="Item">The item.</param>
/// <param name="Name">"Long Sword +1".</param>
/// <param name="Where">"worn" or "pack".</param>
/// <param name="Cost">"to +2: 1,000 gold, 2 gems".</param>
/// <param name="CanUpgrade">Whether another step is possible.</param>
/// <param name="Icon">Picture.</param>
public sealed record UpgradeRow(AVAMMB1.Core.Items.ItemInstance Item, string Name, string Where, string Cost, bool CanUpgrade, Bitmap? Icon);

/// <summary>Formats item descriptions.</summary>
public static class ItemText
{
    /// <summary>Short stats description.</summary>
    /// <param name="d">Item.</param>
    /// <param name="plus">Smithy upgrade.</param>
    public static string Describe(ItemDef d, int plus = 0)
    {
        var parts = new List<string>();
        if (plus > 0)
        {
            parts.Add(d.Kind is ItemKind.Weapon or ItemKind.Missile ? $"smithed +{plus} to hit and damage" : $"smithed +{plus} AC");
        }
        if (d.Kind is ItemKind.Weapon or ItemKind.Missile)
        {
            parts.Add($"Dmg {d.Damage}");
        }
        if (d.ArmorClass != 0)
        {
            parts.Add($"AC +{d.ArmorClass}");
        }
        if (d.HitBonus != 0)
        {
            parts.Add($"Hit +{d.HitBonus}");
        }
        foreach (var (s, v) in d.StatBonuses)
        {
            parts.Add($"{s} +{v}");
        }
        if (d.TwoHanded)
        {
            parts.Add("two-handed");
        }
        if (d.LightRadius > 0)
        {
            parts.Add($"light {d.LightRadius} squares");
        }
        if (d.Kind == ItemKind.Lantern)
        {
            parts.Add(d.FuelCapacity > 0 ? $"holds {d.FuelCapacity} steps of oil" : "never needs oil");
        }
        else if (d.Kind == ItemKind.Oil)
        {
            parts.Add($"+{d.FuelAmount} steps of lantern oil; 2d6 fire when thrown");
        }
        else if (d.Charges > 0)
        {
            parts.Add($"{d.Charges} charges");
        }
        if (parts.Count == 0 && d.Description.Length > 0)
        {
            parts.Add(d.Description);
        }
        return string.Join(", ", parts);
    }
}

/// <summary>Shop: buy and sell.</summary>
public sealed partial class ShopViewModel : BuildingViewModel
{
    private readonly ShopDef _shop;

    /// <summary>Creates the dialog.</summary>
    /// <param name="game">Owner.</param>
    /// <param name="shop">Shop definition.</param>
    public ShopViewModel(GameViewModel game, ShopDef shop) : base(game, shop.Name)
    {
        _shop = shop;
        Session.NoteSeenItems(shop.Stock);
        Feedback = shop.Greeting;
        _selectedMember = game.Party.FirstOrDefault();
        RefreshRows();
    }

    /// <summary>Items for sale.</summary>
    public ObservableCollection<ShopItemViewModel> Stock { get; } = new();

    /// <summary>Selected member's sellable items.</summary>
    public ObservableCollection<ShopItemViewModel> Sellable { get; } = new();

    /// <summary>Member buying / selling.</summary>
    [ObservableProperty]
    private PartyMemberViewModel? _selectedMember;

    partial void OnSelectedMemberChanged(PartyMemberViewModel? value) => RefreshRows();

    /// <summary>What the selected buyer can spend.</summary>
    public string SpendText => SelectedMember is { } m
        ? $"Can spend {Session.State.Available(m.Character)} gold (purse {Session.State.Gold} + {m.Name}'s {m.Character.Gold})"
        : "";

    /// <inheritdoc />
    protected override void RefreshRows()
    {
        Stock.Clear();
        Sellable.Clear();
        var c = SelectedMember?.Character;
        var tex = Game.Services.Textures;
        foreach (var id in _shop.Stock)
        {
            var d = Session.Content.Item(id);
            Stock.Add(new ShopItemViewModel(d, AVAMMB1.Core.Session.TownServices.BuyPrice(_shop, d), tex.Bitmap("Items/" + d.Icon),
                c is null || d.Slot is null || Rulebook.CanUse(c, d), -1,
                c is null ? "" : ItemCompare.Describe(Session.Rules, c, d), c is not null && ItemCompare.IsUpgradeFor(Session.Rules, c, d)));
        }
        if (c is not null)
        {
            for (var i = 0; i < c.Backpack.Count; i++)
            {
                var inst = c.Backpack[i];
                var d = Session.Rules.Def(inst);
                Sellable.Add(new ShopItemViewModel(d, Session.Rules.SellPrice(inst), tex.Bitmap("Items/" + d.Icon), true, i,
                    junk: ItemCompare.IsJunk(Session.Rules, Session.State.Party, d, inst.Plus), name: Session.Rules.ItemName(inst)));
            }
        }
        Upgrades.Clear();
        if (c is not null && _shop.Smithy)
        {
            var items = c.Equipment.Values.Select(i => (Item: i, Where: "worn")).Concat(c.Backpack.Select(i => (Item: i, Where: "pack")));
            foreach (var (item, where) in items)
            {
                var d = Session.Rules.Def(item);
                if (!Rulebook.Upgradable(d))
                {
                    continue;
                }
                var next = item.Plus + 1;
                var cost = item.Plus >= Rulebook.MaxPlus ? "the finest a smith can make"
                    : $"to +{next}: {Rulebook.UpgradeGold(d, next):N0} gold, {Rulebook.UpgradeGems(next)} gem{(next > 1 ? "s" : "")}";
                Upgrades.Add(new UpgradeRow(item, Session.Rules.ItemName(item), where, cost, item.Plus < Rulebook.MaxPlus, tex.Bitmap("Items/" + d.Icon)));
            }
        }
        OnPropertyChanged(nameof(GemsText));
        var junk = Session.Town.Junk();
        JunkText = junk.Count == 0 ? "Sell junk" : $"Sell junk ({junk.Count}, {junk.Sum(j => Session.Rules.SellPrice(j.Item))} gp)";
        JunkTip = junk.Count == 0
            ? "Nobody in the party carries gear they could not use as an upgrade."
            : "Sells every backpack weapon, armour, ring or amulet that nobody in the party could use as an upgrade: " + string.Join(", ", junk.Select(j => $"{j.Def.Name} ({j.Owner.Name})"));
        HasJunk = junk.Count > 0;
        OnPropertyChanged(nameof(JunkText));
        OnPropertyChanged(nameof(JunkTip));
        OnPropertyChanged(nameof(HasJunk));
        OnPropertyChanged(nameof(SpendText));
    }

    [RelayCommand]
    private void Buy(ShopItemViewModel item)
    {
        if (SelectedMember is { } m)
        {
            Report(Session.Town.Buy(_shop, item.Def.Id, m.Character));
        }
    }

    /// <summary>Label for the sell-junk button (count and gold).</summary>
    public string JunkText { get; private set; } = "Sell junk";
    /// <summary>What sell-junk would sell.</summary>
    public string JunkTip { get; private set; } = "";
    /// <summary>Whether there is junk to sell.</summary>
    public bool HasJunk { get; private set; }

    [RelayCommand]
    private void SellJunk() => Report(Session.Town.SellJunk());

    /// <summary>Whether this shop's smith upgrades gear.</summary>
    public bool IsSmithy => _shop.Smithy;

    /// <summary>Showing the smithing list instead of the sell list.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RightTitle), nameof(SmithingLabel))]
    private bool _showSmithing;

    /// <summary>Right-hand column title.</summary>
    public string RightTitle => ShowSmithing ? "Smithing (+1 to +5)" : "Sell from backpack (half price)";

    /// <summary>Toggle button text.</summary>
    public string SmithingLabel => ShowSmithing ? "Back to selling" : "Smithing";

    /// <summary>Gems available for smithing.</summary>
    public string GemsText => $"The party has {Session.State.Gems} gem{(Session.State.Gems == 1 ? "" : "s")}. Each step costs more gold - and one more gem - than the last.";

    /// <summary>The shopper's gear the smith can improve.</summary>
    public ObservableCollection<UpgradeRow> Upgrades { get; } = new();

    [RelayCommand]
    private void ToggleSmithing() => ShowSmithing = !ShowSmithing;

    [RelayCommand]
    private void UpgradeItem(UpgradeRow row)
    {
        if (SelectedMember is { } m)
        {
            Report(Session.Town.Upgrade(_shop, m.Character, row.Item));
        }
    }

    [RelayCommand]
    private void Sell(ShopItemViewModel item)
    {
        if (SelectedMember is { } m)
        {
            Report(Session.Town.Sell(m.Character, item.Index));
        }
    }
}
