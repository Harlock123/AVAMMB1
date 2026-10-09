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

/// <summary>An item row on the character sheet.</summary>
/// <param name="Slot">Equipment slot (null for backpack).</param>
/// <param name="Index">Backpack index (-1 for equipment).</param>
/// <param name="Name">Label.</param>
/// <param name="Detail">Stats.</param>
/// <param name="Icon">Icon.</param>
public sealed record ItemRow(EquipSlot? Slot, int Index, string Name, string Detail, Bitmap? Icon)
{
    /// <summary>Slot label.</summary>
    public string SlotLabel => Slot?.ToString() ?? "";
}

/// <summary>Character sheet with stats, equipment and backpack.</summary>
public sealed partial class CharacterSheetViewModel : ViewModelBase
{
    private readonly GameViewModel _game;

    /// <summary>Creates the sheet.</summary>
    /// <param name="game">Owner.</param>
    /// <param name="index">Party index to show first.</param>
    public CharacterSheetViewModel(GameViewModel game, int index)
    {
        _game = game;
        _index = Math.Clamp(index, 0, Math.Max(0, game.Party.Count - 1));
        Load();
    }

    private GameSession Session => _game.Services.Session;

    /// <summary>Party.</summary>
    public ObservableCollection<PartyMemberViewModel> Party => _game.Party;

    [ObservableProperty]
    private int _index;

    /// <summary>Selected item row.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection), nameof(SelectedDescription))]
    private ItemRow? _selected;

    /// <summary>Feedback.</summary>
    [ObservableProperty]
    private string _feedback = "";

    /// <summary>Current member.</summary>
    public PartyMemberViewModel Member => _game.Party[Index];
    /// <summary>Current character.</summary>
    public Character Character => Member.Character;
    /// <summary>Equipment rows.</summary>
    public ObservableCollection<ItemRow> Equipment { get; } = new();
    /// <summary>Backpack rows.</summary>
    public ObservableCollection<ItemRow> Backpack { get; } = new();
    /// <summary>Attribute lines.</summary>
    public ObservableCollection<string> Stats { get; } = new();
    /// <summary>Known spells.</summary>
    public ObservableCollection<string> Spells { get; } = new();
    /// <summary>Whether an item is selected.</summary>
    public bool HasSelection => Selected is not null;
    /// <summary>Description of the selected item.</summary>
    public string SelectedDescription => Selected is null ? "Select an item." : $"{Selected.Name}: {Selected.Detail}";

    /// <summary>Header line.</summary>
    public string Header => $"{Character.Name} - {Character.Sex} {Session.Content.Race(Character.Race).Name} {Session.Content.Class(Character.Class).Name}, {Character.Alignment}";

    /// <summary>Summary lines.</summary>
    public string Summary
    {
        get
        {
            var r = Session.Rules;
            var next = r.XpForNextLevel(Character);
            return $"Level {Character.Level}   XP {Character.Experience} / {next}\n" +
                   $"HP {Character.Hp}/{Character.MaxHp}   SP {Character.Sp}/{Character.MaxSp}   AC {r.ArmorClass(Character)}\n" +
                   $"To-hit +{r.MeleeAttackBonus(Character)} (missile +{r.MissileAttackBonus(Character)})   Attacks {r.AttacksPerRound(Character)}\n" +
                   $"Food {Character.Food}   Condition: {Character.StatusText}" +
                   (r.Thievery(Character) > 0 ? $"   Thievery {r.Thievery(Character)}%" : "");
        }
    }

    partial void OnIndexChanged(int value) => Load();

    private void Load()
    {
        var c = Character;
        var tex = _game.Services.Textures;
        Equipment.Clear();
        foreach (var slot in Enum.GetValues<EquipSlot>())
        {
            if (c.Equipment.TryGetValue(slot, out var item))
            {
                var d = Session.Rules.Def(item);
                Equipment.Add(new ItemRow(slot, -1, d.Name, ItemText.Describe(d), tex.Bitmap("Items/" + d.Icon)));
            }
            else
            {
                Equipment.Add(new ItemRow(slot, -1, "-", "", null));
            }
        }
        Backpack.Clear();
        for (var i = 0; i < c.Backpack.Count; i++)
        {
            var d = Session.Rules.Def(c.Backpack[i]);
            var extra = c.Backpack[i].Charges > 0 ? $" [{c.Backpack[i].Charges}]" : "";
            Backpack.Add(new ItemRow(null, i, d.Name + extra, ItemText.Describe(d) + (d.Slot is not null && !Rulebook.CanUse(c, d) ? " (cannot use)" : ""), tex.Bitmap("Items/" + d.Icon)));
        }
        Stats.Clear();
        foreach (var s in Enum.GetValues<Stat>())
        {
            var v = Session.Rules.Stat(c, s);
            var b = Rulebook.StatBonus(v);
            Stats.Add($"{s,-12} {v,3}  ({(b >= 0 ? "+" : "")}{b})");
        }
        Spells.Clear();
        foreach (var sp in Session.Rules.KnownSpells(c))
        {
            Spells.Add($"L{sp.Level} {sp.Name} ({sp.Cost} SP) - {sp.Description}");
        }
        Selected = null;
        OnPropertyChanged(nameof(Member));
        OnPropertyChanged(nameof(Character));
        OnPropertyChanged(nameof(Header));
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(GoldLine));
    }

    private void Done(InventoryResult r)
    {
        Feedback = r.Message;
        _game.Services.Audio.PlaySfx(r.Success ? "equip" : "bump");
        _game.Refresh();
        Load();
    }

    /// <summary>Gold line for the current character.</summary>
    public string GoldLine => $"{Character.Name} carries {Character.Gold} gold. Party purse: {Session.State.Gold}.";

    /// <summary>Amount for deposit / withdraw.</summary>
    [ObservableProperty]
    private decimal? _goldAmount = 100;

    private void GoldDone(List<GameMessage> log)
    {
        _game.AddMessages(log);
        Feedback = string.Join(" ", log.Select(m => m.Text));
        _game.Refresh();
        OnPropertyChanged(nameof(GoldLine));
    }

    private int Amount => (int)Math.Max(0, GoldAmount ?? 0);

    [RelayCommand]
    private void DepositGold() => GoldDone(Session.Town.Deposit(Character, Amount));

    [RelayCommand]
    private void DepositAllGold() => GoldDone(Session.Town.Deposit(Character, Character.Gold));

    [RelayCommand]
    private void WithdrawGold() => GoldDone(Session.Town.Withdraw(Character, Amount));

    [RelayCommand]
    private void PoolAllGold() => GoldDone(Session.Town.PoolAll());

    [RelayCommand]
    private void ShareGold() => GoldDone(Session.Town.ShareEvenly());

    [RelayCommand]
    private void Next() => Index = (Index + 1) % _game.Party.Count;

    [RelayCommand]
    private void Previous() => Index = (Index + _game.Party.Count - 1) % _game.Party.Count;

    [RelayCommand]
    private void Select(ItemRow row) => Selected = row.Slot is not null && row.Name == "-" ? null : row;

    [RelayCommand]
    private void EquipOrRemove()
    {
        if (Selected is null)
        {
            return;
        }
        Done(Selected.Slot is { } slot ? Session.Inventory.Unequip(Character, slot) : Session.Inventory.Equip(Character, Selected.Index));
    }

    [RelayCommand]
    private void Use()
    {
        if (Selected is null || Selected.Index < 0)
        {
            return;
        }
        var result = Session.Spells.UseItem(Character, Selected.Index, Session.State, null, Index, null);
        var step = Session.AfterExploreMagic(result);
        _game.AddMessages(step.Messages);
        Feedback = string.Join(" ", result.Messages.Select(m => m.Text));
        _game.Refresh();
        Load();
    }

    [RelayCommand]
    private void Give(PartyMemberViewModel to)
    {
        if (Selected is null || Selected.Index < 0 || ReferenceEquals(to.Character, Character))
        {
            return;
        }
        Done(Session.Inventory.Give(Character, Selected.Index, to.Character));
    }

    [RelayCommand]
    private void Drop()
    {
        if (Selected is null || Selected.Index < 0)
        {
            return;
        }
        Done(Session.Inventory.Discard(Character, Selected.Index));
    }

    [RelayCommand]
    private void Close() => _game.CloseOverlay();

    /// <inheritdoc />
    public override bool HandleKey(Key key)
    {
        switch (key)
        {
            case Key.Right or Key.Tab: Next(); return true;
            case Key.Left: Previous(); return true;
            case >= Key.D1 and <= Key.D6 when key - Key.D1 < _game.Party.Count: Index = key - Key.D1; return true;
            case Key.I or Key.C: Close(); return true;
            default: return false;
        }
    }
}

/// <summary>A caster choice for exploration spellcasting.</summary>
/// <param name="Index">Party index.</param>
/// <param name="Label">Label.</param>
public sealed record CasterOption(int Index, string Label);

/// <summary>Spell option.</summary>
/// <param name="Spell">Spell.</param>
/// <param name="Label">Label.</param>
/// <param name="Enabled">Whether castable now.</param>
public sealed record SpellOption(SpellDef Spell, string Label, bool Enabled);

/// <summary>Out-of-combat spellcasting dialog.</summary>
public sealed partial class SpellCastViewModel : ViewModelBase
{
    private readonly GameViewModel _game;

    /// <summary>Creates the dialog.</summary>
    /// <param name="game">Owner.</param>
    /// <param name="casterIndex">Preselected caster.</param>
    public SpellCastViewModel(GameViewModel game, int? casterIndex)
    {
        _game = game;
        var rules = game.Services.Session.Rules;
        var party = game.Services.Session.State.Party;
        for (var i = 0; i < party.Count; i++)
        {
            if (rules.KnownSpells(party[i]).Any())
            {
                Casters.Add(new CasterOption(i, $"{party[i].Name} ({party[i].Sp}/{party[i].MaxSp} SP)"));
            }
        }
        SelectedCaster = Casters.FirstOrDefault(c => c.Index == casterIndex) ?? Casters.FirstOrDefault();
        Feedback = Casters.Count == 0 ? "No one in the party can cast spells yet." : "Choose a caster and a spell.";
    }

    /// <summary>Casters.</summary>
    public ObservableCollection<CasterOption> Casters { get; } = new();
    /// <summary>Spells of the selected caster.</summary>
    public ObservableCollection<SpellOption> SpellList { get; } = new();
    /// <summary>Party (for ally targets).</summary>
    public ObservableCollection<PartyMemberViewModel> Party => _game.Party;

    /// <summary>Caster.</summary>
    [ObservableProperty]
    private CasterOption? _selectedCaster;

    /// <summary>Spell waiting for an ally target.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NeedsAlly))]
    private SpellDef? _pendingSpell;

    /// <summary>Feedback.</summary>
    [ObservableProperty]
    private string _feedback = "";

    /// <summary>Whether an ally must be chosen.</summary>
    public bool NeedsAlly => PendingSpell is not null;

    partial void OnSelectedCasterChanged(CasterOption? value) => LoadSpells();

    private void LoadSpells()
    {
        SpellList.Clear();
        PendingSpell = null;
        if (SelectedCaster is null)
        {
            return;
        }
        var s = _game.Services.Session;
        var c = s.State.Party[SelectedCaster.Index];
        foreach (var sp in s.Rules.KnownSpells(c))
        {
            var reason = s.Spells.CanCast(c, sp, inCombat: false);
            SpellList.Add(new SpellOption(sp, $"L{sp.Level} {sp.Name} ({sp.Cost} SP) - {sp.Description}", reason is null));
        }
    }

    [RelayCommand]
    private void Cast(SpellOption option)
    {
        if (!option.Enabled)
        {
            Feedback = _game.Services.Session.Spells.CanCast(_game.Services.Session.State.Party[SelectedCaster!.Index], option.Spell, false) ?? "";
            return;
        }
        if (option.Spell.Target == TargetKind.Ally)
        {
            PendingSpell = option.Spell;
            Feedback = $"Cast {option.Spell.Name} on whom?";
            return;
        }
        Do(option.Spell, -1);
    }

    [RelayCommand]
    private void Target(PartyMemberViewModel member)
    {
        if (PendingSpell is { } sp)
        {
            Do(sp, member.Index);
        }
    }

    private void Do(SpellDef spell, int ally)
    {
        var s = _game.Services.Session;
        var caster = s.State.Party[SelectedCaster!.Index];
        var result = s.Spells.Cast(caster, spell, s.State, null, ally, null);
        var step = s.AfterExploreMagic(result);
        _game.AddMessages(step.Messages);
        Feedback = string.Join(" ", result.Messages.Select(m => m.Text));
        var idx = SelectedCaster.Index;
        Casters[Casters.IndexOf(SelectedCaster)] = new CasterOption(idx, $"{caster.Name} ({caster.Sp}/{caster.MaxSp} SP)");
        SelectedCaster = Casters.First(c => c.Index == idx);
        _game.Refresh();
        if (step.MapChanged)
        {
            _game.CloseOverlay();
        }
    }

    [RelayCommand]
    private void Close() => _game.CloseOverlay();
}

/// <summary>Full-screen automap.</summary>
/// <param name="game">Owner.</param>
public sealed partial class AutomapViewModel(GameViewModel game) : ViewModelBase
{
    /// <summary>Map snapshot.</summary>
    public MapSnapshot? MapInfo => game.MapInfo;
    /// <summary>Map name.</summary>
    public string Title => game.LocationText;
    /// <summary>Position.</summary>
    public string Position => game.CompassText;

    [RelayCommand]
    private void Close() => game.CloseOverlay();

    /// <inheritdoc />
    public override bool HandleKey(Key key)
    {
        if (key is Key.M or Key.Enter)
        {
            Close();
            return true;
        }
        return false;
    }
}

/// <summary>In-game menu.</summary>
/// <param name="game">Owner.</param>
public sealed partial class GameMenuViewModel(GameViewModel game) : ViewModelBase
{
    [RelayCommand]
    private void Resume() => game.CloseOverlay();

    [RelayCommand]
    private void Save() => game.Overlay = new SaveLoadViewModel(game.Main, saving: true, onClose: () => game.Overlay = this);

    [RelayCommand]
    private void Load() => game.Overlay = new SaveLoadViewModel(game.Main, saving: false, onClose: () => game.Overlay = this);

    [RelayCommand]
    private void Settings()
    {
        game.Overlay = null;
        game.Main.ShowSettings(game);
    }

    [RelayCommand]
    private void QuitToTitle() => game.Main.ShowTitle();
}
