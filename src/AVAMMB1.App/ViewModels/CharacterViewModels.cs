using System.Collections.ObjectModel;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using AVAMMB1.Core.Characters;
using AVAMMB1.Core.Combat;
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
                var fuel = d.Kind == ItemKind.Lantern && d.FuelCapacity > 0 ? $" [oil {item.Charges}/{d.FuelCapacity}]" : "";
                Equipment.Add(new ItemRow(slot, -1, Session.Rules.ItemName(item) + fuel, ItemText.Describe(d, item.Plus), tex.Bitmap("Items/" + d.Icon)));
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
            var extra = d.Kind == ItemKind.Lantern ? (d.FuelCapacity > 0 ? $" [oil {c.Backpack[i].Charges}/{d.FuelCapacity}]" : "")
                : c.Backpack[i].Charges > 0 ? $" [{c.Backpack[i].Charges}]" : "";
            Backpack.Add(new ItemRow(null, i, Session.Rules.ItemName(c.Backpack[i]) + extra, ItemText.Describe(d, c.Backpack[i].Plus) + (d.Slot is not null && !Rulebook.CanUse(c, d) ? " (cannot use)" : ""), tex.Bitmap("Items/" + d.Icon)));
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
        OnPropertyChanged(nameof(CanMoveLeft));
        OnPropertyChanged(nameof(CanMoveRight));
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

    /// <summary>Whether the shown character can move one place forward / back in the marching order.</summary>
    public bool CanMoveLeft => Index > 0;

    /// <inheritdoc cref="CanMoveLeft" />
    public bool CanMoveRight => Index < _game.Party.Count - 1;

    [RelayCommand]
    private void MoveLeft() => MoveTo(Index - 1);

    [RelayCommand]
    private void MoveRight() => MoveTo(Index + 1);

    private void MoveTo(int place)
    {
        if (place < 0 || place >= _game.Party.Count || _game.MoveMember(Index, place, fromSheet: true) is not { } text)
        {
            return;
        }
        Index = place; // keep showing the same character
        Feedback = text;
    }

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
        if (Session.Rules.Def(Character.Backpack[Selected.Index]) is { Slot: not null, UseSpell: null })
        {
            // "Using" a lantern, a weapon or armour means putting it on.
            Done(Session.Inventory.Equip(Character, Selected.Index));
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
            case Key.OemOpenBrackets: MoveLeft(); return true;
            case Key.OemCloseBrackets: MoveRight(); return true;
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
public sealed partial class AutomapViewModel : ViewModelBase
{
    private readonly GameViewModel _game;

    /// <summary>Creates the screen.</summary>
    /// <param name="game">Owner.</param>
    /// <param name="editNote">Whether to start with the note box focused (the Note command).</param>
    public AutomapViewModel(GameViewModel game, bool editNote)
    {
        _game = game;
        EditNote = editNote;
        Select(new Avalonia.PixelPoint(State.X, State.Y));
    }

    private GameState State => _game.Services.Session.State;
    private string MapId => _game.Services.Session.CurrentMap.Id;

    /// <summary>Whether the view should focus the note box when it opens.</summary>
    public bool EditNote { get; }
    /// <summary>Map snapshot.</summary>
    /// <summary>Where open quests lead, on this map first ("The Silent Choir: Loremaster's Hall, Thornwick").</summary>
    public IReadOnlyList<string> Goals => (_game.MapInfo?.Goals ?? [])
        .OrderByDescending(g => g.MapId == _game.Services.Session.State.MapId)
        .Select(g => (g.MapId == _game.Services.Session.State.MapId ? "\u25C6 " : "") + $"{g.Quest}: {JournalQuest.GoalText(g)}")
        .ToList();

    /// <summary>Whether any quest goal is known.</summary>
    public bool HasGoals => Goals.Count > 0;

    public MapSnapshot? MapInfo => _game.MapInfo;
    /// <summary>Map name.</summary>
    public string Title => _game.LocationText;
    /// <summary>Position.</summary>
    public string Position => _game.CompassText;

    /// <summary>Square whose note is being edited.</summary>
    [ObservableProperty]
    private Avalonia.PixelPoint? _selectedCell;

    /// <summary>Note text for the selected square.</summary>
    [ObservableProperty]
    private string _noteText = "";

    /// <summary>Repaint counter for the map.</summary>
    [ObservableProperty]
    private int _revision;

    /// <summary>Label above the note box.</summary>
    public string NoteLabel => SelectedCell is { } c
        ? (c.X == State.X && c.Y == State.Y ? $"Note for your square ({c.X},{c.Y})" : $"Note for square ({c.X},{c.Y})")
        : "Click a square to write a note";

    /// <summary>All notes on this map.</summary>
    public ObservableCollection<string> Notes { get; } = new();

    /// <summary>Selects a square (clicked on the map).</summary>
    /// <param name="cell">Square.</param>
    public void Select(Avalonia.PixelPoint cell)
    {
        SelectedCell = cell;
        NoteText = State.NoteAt(MapId, cell.X, cell.Y) ?? "";
        OnPropertyChanged(nameof(NoteLabel));
        RefreshNotes();
    }

    private void RefreshNotes()
    {
        Notes.Clear();
        var prefix = MapId + ":";
        foreach (var (key, text) in State.MapNotes.Where(n => n.Key.StartsWith(prefix, StringComparison.Ordinal)).OrderBy(n => n.Key, StringComparer.Ordinal))
        {
            var xy = key[prefix.Length..].Replace(':', ',');
            Notes.Add($"({xy})  {text}");
        }
        Revision++;
    }

    [RelayCommand]
    private void SaveNote()
    {
        if (SelectedCell is { } c)
        {
            State.SetNote(MapId, c.X, c.Y, NoteText);
            RefreshNotes();
        }
    }

    [RelayCommand]
    private void DeleteNote()
    {
        if (SelectedCell is { } c)
        {
            State.SetNote(MapId, c.X, c.Y, null);
            NoteText = "";
            RefreshNotes();
        }
    }

    [RelayCommand]
    private void Close() => _game.CloseOverlay();

    /// <inheritdoc />
    public override bool HandleKey(Key key)
    {
        if (key is Key.M or Key.N or Key.Enter)
        {
            Close();
            return true;
        }
        return false;
    }
}

/// <summary>The quest journal: quests the party knows about, and the signs and clues it has read.</summary>
public sealed partial class JournalViewModel : ViewModelBase
{
    private readonly GameViewModel _game;

    /// <summary>Creates the screen.</summary>
    /// <param name="game">Owner.</param>
    public JournalViewModel(GameViewModel game)
    {
        _game = game;
        var state = game.Services.Session.State;
        var content = game.Services.Content;
        var goals = game.Services.Settings.QuestMarkers ? game.Services.Session.QuestGoals() : [];
        Quests = QuestJournal.Quests(state, content).Select(q => new JournalQuest(q, goals.FirstOrDefault(g => g.QuestId == q.Id))).ToList();
        Discoveries = QuestJournal.Discoveries(state, content);
        var session = game.Services.Session;
        var tex = game.Services.Textures;
        Bestiary = state.KnownMonsters.Where(content.Monsters.ContainsKey).Select(id => content.Monsters[id])
            .OrderBy(m => m.Level).ThenBy(m => m.Name, StringComparer.Ordinal)
            .Select(m => new BestiaryEntry(tex.Bitmap("Monsters/" + m.Sprite), m.Name, MonsterLore.Stats(m), MonsterLore.Attacks(m),
                MonsterLore.Defenses(m), session.WhereFound(m.Id) is { Count: > 0 } where ? "Found in " + string.Join(", ", where) : "",
                state.Kills.GetValueOrDefault(m.Id), m.Description))
            .ToList();
        BestiaryCount = $"{Bestiary.Count} of {content.Monsters.Count} creatures known. Defeat a creature to add it.";
        Items = state.SeenItems.Where(content.Items.ContainsKey).Select(id => content.Items[id])
            .OrderBy(i => i.Kind).ThenBy(i => i.Price).ThenBy(i => i.Name, StringComparer.Ordinal)
            .Select(i => new ItemEntry(tex.Bitmap("Items/" + i.Icon), i.Name, KindName(i.Kind), ItemText.Describe(i),
                i.Description, i.Kind == ItemKind.Quest ? "" : $"worth {Rulebook.SellPrice(i)} gold",
                i.Classes.Count == 0 ? "" : "Classes: " + string.Join(", ", i.Classes.Select(c => content.Classes.TryGetValue(c, out var cd) ? cd.Name : c))))
            .ToList();
        ItemsCount = $"{Items.Count} of {content.Items.Count} items seen. Items you carry or see in shops are added.";
        game.CountPlayTime();
        long Stat(string k) => state.Stats.GetValueOrDefault(k);
        var played = TimeSpan.FromSeconds(state.PlaySeconds);
        var bosses = content.Monsters.Values.Where(m => m.Boss).ToList();
        Statistics =
        [
            new("Difficulty", state.Difficulty + (state.Survival ? ", survival mode" : "")),
            new("Days in the field", state.Day.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new("Time played", SlotRow.PlayTimeText(played)),
            new("Steps taken", state.Steps.ToString("N0", System.Globalization.CultureInfo.CurrentCulture)),
            new("Monsters slain", state.Kills.Values.Sum().ToString("N0", System.Globalization.CultureInfo.CurrentCulture)),
            new("Unique monsters defeated", $"{bosses.Count(b => state.Kills.GetValueOrDefault(b.Id) > 0)} of {bosses.Count}"),
            new("Battles won / fled", $"{Stat(Chronicle.Keys.BattlesWon)} / {Stat(Chronicle.Keys.BattlesFled)}"),
            new("Companions fallen in battle", Stat(Chronicle.Keys.Deaths).ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new("Gold found", Stat(Chronicle.Keys.GoldFound).ToString("N0", System.Globalization.CultureInfo.CurrentCulture)),
            new("Most gold held", Stat(Chronicle.Keys.MostGold).ToString("N0", System.Globalization.CultureInfo.CurrentCulture)),
            new("Chests opened", Stat(Chronicle.Keys.Chests).ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new("Secret doors found", Stat(Chronicle.Keys.Secrets).ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new("Locks picked", Stat(Chronicle.Keys.Locks).ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new("Spells cast", Stat(Chronicle.Keys.Spells).ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new("Nights at inns", Stat(Chronicle.Keys.InnNights).ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new("Deepest level of the Depths Below", state.DeepestDepth == 0 ? "-" : state.DeepestDepth.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        ];
        Achievements = Chronicle.Achievements.Select(a => new AchievementRow(a.Title, a.Description, state.Achievements.Contains(a.Id))).ToList();
        AchievementCount = $"Achievements: {Achievements.Count(a => a.Earned)} of {Achievements.Count}";
    }

    private static string KindName(ItemKind k) => k switch
    {
        ItemKind.Missile => "missile weapon",
        ItemKind.Quest => "quest item",
        _ => k.ToString().ToLowerInvariant(),
    };

    /// <summary>Selected tab (0 quests, 1 bestiary, 2 items, 3 clues...).</summary>
    [ObservableProperty]
    private int _tab;

    /// <summary>Monsters the party has defeated.</summary>
    public IReadOnlyList<BestiaryEntry> Bestiary { get; }
    /// <summary>The Chronicle's statistics.</summary>
    public IReadOnlyList<StatisticRow> Statistics { get; }
    /// <summary>Every achievement, earned or not.</summary>
    public IReadOnlyList<AchievementRow> Achievements { get; }
    /// <summary>"Achievements: 5 of 22".</summary>
    public string AchievementCount { get; }
    /// <summary>"12 of 103 creatures known".</summary>
    public string BestiaryCount { get; }
    /// <summary>Items the party has carried or seen.</summary>
    public IReadOnlyList<ItemEntry> Items { get; }
    /// <summary>"40 of 106 items seen".</summary>
    public string ItemsCount { get; }

    /// <summary>Known quests.</summary>
    public IReadOnlyList<JournalQuest> Quests { get; }
    /// <summary>Clues read.</summary>
    public IReadOnlyList<Discovery> Discoveries { get; }
    /// <summary>Whether there are no quests yet.</summary>
    public bool NoQuests => Quests.Count == 0;
    /// <summary>Whether there are no clues yet.</summary>
    public bool NoDiscoveries => Discoveries.Count == 0;

    [RelayCommand]
    private void Close() => _game.CloseOverlay();

    /// <inheritdoc />
    public override bool HandleKey(Key key)
    {
        if (key is Key.J or Key.Enter)
        {
            Close();
            return true;
        }
        return false;
    }
}

/// <summary>A quest row in the journal.</summary>
/// <param name="Quest">Quest.</param>
/// <param name="Goal">Where it leads next, if known.</param>
public sealed record JournalQuest(QuestEntry Quest, QuestGoal? Goal = null)
{
    /// <summary>"Next: Loremaster's Hall, Thornwick (7,9)".</summary>
    public string Next => Goal is null || Quest.Done ? "" : "Next: " + GoalText(Goal);
    /// <summary>Whether a next place is known.</summary>
    public bool HasNext => Next.Length > 0;

    /// <summary>A place in words.</summary>
    /// <param name="g">Goal.</param>
    public static string GoalText(QuestGoal g) =>
        (g.Place == g.MapName ? g.MapName : $"{g.Place}, {g.MapName}") + $" ({g.X},{g.Y})";

    /// <summary>Title with status.</summary>
    public string Heading => Quest.Title + (Quest.Done ? "  (complete)" : Quest.Main ? "  (main quest)" : "");
    /// <summary>The current goal.</summary>
    public string Current => Quest.Entries[^1];
    /// <summary>Earlier entries, oldest first.</summary>
    public IReadOnlyList<string> Earlier => Quest.Entries.Take(Quest.Entries.Count - 1).ToList();
    /// <summary>Whether there is history to show.</summary>
    public bool HasEarlier => Quest.Entries.Count > 1;
    /// <summary>Whether the quest is complete.</summary>
    public bool Done => Quest.Done;
}

/// <summary>A Chronicle statistic.</summary>
/// <param name="Label">What is counted.</param>
/// <param name="Value">Count.</param>
public sealed record StatisticRow(string Label, string Value);

/// <summary>A Chronicle achievement.</summary>
/// <param name="Title">Title.</param>
/// <param name="Description">How to earn it.</param>
/// <param name="Earned">Whether it is earned.</param>
public sealed record AchievementRow(string Title, string Description, bool Earned)
{
    /// <summary>Tick or empty box.</summary>
    public string Mark => Earned ? "\u2714" : "\u25A1";
}

/// <summary>A bestiary page.</summary>
/// <param name="Sprite">Picture.</param>
/// <param name="Name">Name.</param>
/// <param name="Stats">Level, HP, AC...</param>
/// <param name="Attacks">How it fights.</param>
/// <param name="Defenses">Resistances.</param>
/// <param name="Found">Explored maps where it lives.</param>
/// <param name="Kills">How many the party has slain.</param>
/// <param name="Description">Flavour text.</param>
public sealed record BestiaryEntry(Bitmap? Sprite, string Name, string Stats, string Attacks, string Defenses, string Found, int Kills, string Description)
{
    /// <summary>"Slain: 14".</summary>
    public string KillText => Kills > 0 ? $"Slain: {Kills}" : "Slain: before records were kept";
    /// <summary>Whether to show where it is found.</summary>
    public bool HasFound => Found.Length > 0;
}

/// <summary>An item compendium page.</summary>
/// <param name="Icon">Picture.</param>
/// <param name="Name">Name.</param>
/// <param name="Kind">"weapon", "ring"...</param>
/// <param name="Stats">Damage, AC, bonuses.</param>
/// <param name="Description">Flavour text.</param>
/// <param name="Value">Sale value.</param>
/// <param name="Classes">Who can use it.</param>
public sealed record ItemEntry(Bitmap? Icon, string Name, string Kind, string Stats, string Description, string Value, string Classes)
{
    /// <summary>"weapon · Dmg 1d8 · worth 25 gold".</summary>
    public string Summary => string.Join(" · ", new[] { Kind, Stats, Value }.Where(s => s.Length > 0));
    /// <summary>Whether a class list is shown.</summary>
    public bool HasClasses => Classes.Length > 0;
}

/// <summary>In-game menu.</summary>
/// <param name="game">Owner.</param>
public sealed partial class GameMenuViewModel(GameViewModel game) : ViewModelBase
{
    [RelayCommand]
    private void Resume() => game.CloseOverlay();

    [RelayCommand]
    private void Travel()
    {
        game.CloseOverlay();
        game.Travel();
    }

    [RelayCommand]
    private void Describe()
    {
        game.CloseOverlay();
        game.Look();
    }

    [RelayCommand]
    private void Journal() => game.Overlay = new JournalViewModel(game);

    [RelayCommand]
    private void Help() => game.Overlay = new HelpViewModel(game.Services.Settings, () => game.Overlay = this);

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
    private void QuitToTitle()
    {
        game.AutoSave(null); // an ironman run is saved as it is left (autosave rules apply otherwise)
        game.Main.ShowTitle();
    }
}
