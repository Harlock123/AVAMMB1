using System.Collections.ObjectModel;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using AVAMMB1.Core.Characters;
using AVAMMB1.Core.Content;
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
        Feedback = "Priests in white robes offer healing - for a donation.";
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

    [RelayCommand]
    private void LeaveMember(PartyMemberViewModel m)
    {
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

    /// <inheritdoc />
    protected override void RefreshRows()
    {
        Roster.Clear();
        foreach (var c in Session.State.Roster)
        {
            Roster.Add(c);
        }
        OnPropertyChanged(nameof(StayLabel));
    }
}

/// <summary>An item offered or owned in a shop.</summary>
public sealed class ShopItemViewModel
{
    /// <summary>Creates the row.</summary>
    /// <param name="def">Item.</param>
    /// <param name="price">Price shown.</param>
    /// <param name="icon">Icon.</param>
    /// <param name="usable">Whether the selected character can use it.</param>
    /// <param name="index">Backpack index (sell rows).</param>
    public ShopItemViewModel(ItemDef def, int price, Bitmap? icon, bool usable, int index = -1)
    {
        Def = def;
        Price = price;
        Icon = icon;
        Usable = usable;
        Index = index;
    }

    /// <summary>Item.</summary>
    public ItemDef Def { get; }
    /// <summary>Name.</summary>
    public string Name => Def.Name;
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

/// <summary>Formats item descriptions.</summary>
public static class ItemText
{
    /// <summary>Short stats description.</summary>
    /// <param name="d">Item.</param>
    public static string Describe(ItemDef d)
    {
        var parts = new List<string>();
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
        if (d.Charges > 0)
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
                c is null || d.Slot is null || Rulebook.CanUse(c, d)));
        }
        if (c is not null)
        {
            for (var i = 0; i < c.Backpack.Count; i++)
            {
                var d = Session.Rules.Def(c.Backpack[i]);
                Sellable.Add(new ShopItemViewModel(d, Rulebook.SellPrice(d), tex.Bitmap("Items/" + d.Icon), true, i));
            }
        }
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

    [RelayCommand]
    private void Sell(ShopItemViewModel item)
    {
        if (SelectedMember is { } m)
        {
            Report(Session.Town.Sell(m.Character, item.Index));
        }
    }
}
