using System.Collections.ObjectModel;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using AVAMMB1.Core.Characters;
using AVAMMB1.Core.Content;
using AVAMMB1.Core.Rules;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AVAMMB1.App.ViewModels;

/// <summary>A selectable option (race, class, sex, alignment).</summary>
public sealed partial class ChoiceViewModel : ObservableObject
{
    /// <summary>Creates a choice.</summary>
    /// <param name="id">Identifier.</param>
    /// <param name="name">Display name.</param>
    /// <param name="description">Tooltip text.</param>
    public ChoiceViewModel(string id, string name, string description = "")
    {
        Id = id;
        Name = name;
        Description = description;
    }

    /// <summary>Identifier.</summary>
    public string Id { get; }
    /// <summary>Display name.</summary>
    public string Name { get; }
    /// <summary>Description.</summary>
    public string Description { get; }

    /// <summary>Whether it can currently be chosen.</summary>
    [ObservableProperty]
    private bool _isEnabled = true;
}

/// <summary>One attribute row in the creation screen.</summary>
/// <param name="Name">Attribute name.</param>
/// <param name="Raw">Rolled value.</param>
/// <param name="Final">Value after race modifiers.</param>
/// <param name="Modifier">Race modifier text.</param>
public sealed record StatRow(string Name, int Raw, int Final, string Modifier);

/// <summary>Character and party creation.</summary>
public sealed partial class PartyCreationViewModel : ViewModelBase
{
    private readonly MainViewModel _main;
    private readonly bool _recruitMode;
    private Dictionary<Stat, int> _raw = new();

    /// <summary>Creates the screen.</summary>
    /// <param name="main">Root view model.</param>
    /// <param name="recruitMode">True when recruiting at an inn during a game.</param>
    public PartyCreationViewModel(MainViewModel main, bool recruitMode)
    {
        _main = main;
        _recruitMode = recruitMode;
        var db = main.Services.Content;
        Races = db.Races.Values.Select(r => new ChoiceViewModel(r.Id, r.Name, r.Description)).ToList();
        Classes = db.Classes.Values.Select(c => new ChoiceViewModel(c.Id, c.Name, c.Description + "\nRequires: " + Requirements(c))).ToList();
        Sexes = Enum.GetValues<Sex>().Select(s => new ChoiceViewModel(s.ToString(), s.ToString())).ToList();
        Alignments = Enum.GetValues<Alignment>().Select(a => new ChoiceViewModel(a.ToString(), a.ToString())).ToList();
        _selectedRace = Races[0];
        _selectedSex = Sexes[0];
        _selectedAlignment = Alignments[1];
        Roll();
    }

    private static string Requirements(ClassDef c)
    {
        var parts = c.Requirements.Select(r => $"{r.Key} {r.Value}+").ToList();
        if (c.AllowedAlignments.Count > 0)
        {
            parts.Add(string.Join("/", c.AllowedAlignments) + " alignment");
        }
        return parts.Count == 0 ? "nothing" : string.Join(", ", parts);
    }

    /// <summary>Screen title.</summary>
    public string Title => _recruitMode ? "Recruit an Adventurer" : "Create Your Party";
    /// <summary>Whether this is the in-game recruit flow.</summary>
    public bool RecruitMode => _recruitMode;
    /// <summary>Races.</summary>
    public IReadOnlyList<ChoiceViewModel> Races { get; }
    /// <summary>Classes.</summary>
    public IReadOnlyList<ChoiceViewModel> Classes { get; }
    /// <summary>Sexes.</summary>
    public IReadOnlyList<ChoiceViewModel> Sexes { get; }
    /// <summary>Alignments.</summary>
    public IReadOnlyList<ChoiceViewModel> Alignments { get; }
    /// <summary>Characters created so far.</summary>
    public ObservableCollection<Character> Created { get; } = new();
    /// <summary>Attribute rows.</summary>
    public ObservableCollection<StatRow> StatRows { get; } = new();

    /// <summary>Character name.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddCharacterCommand))]
    private string _name = "";

    /// <summary>Selected race.</summary>
    [ObservableProperty]
    private ChoiceViewModel _selectedRace;

    /// <summary>Selected class.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddCharacterCommand))]
    private ChoiceViewModel? _selectedClass;

    /// <summary>Selected sex.</summary>
    [ObservableProperty]
    private ChoiceViewModel _selectedSex;

    /// <summary>Selected alignment.</summary>
    [ObservableProperty]
    private ChoiceViewModel _selectedAlignment;

    /// <summary>Status / validation text.</summary>
    [ObservableProperty]
    private string _status = "";

    /// <summary>Portrait preview.</summary>
    public Bitmap? Portrait => _main.Services.Portrait(SelectedRace.Id, Enum.Parse<Sex>(SelectedSex.Id), SelectedClass?.Id, Hair, Beard);

    /// <summary>Chosen hairstyle (null: the class's helmet or hood).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Portrait), nameof(HairLabel))]
    private string? _hair;

    /// <summary>Chosen beard (null: none).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Portrait), nameof(BeardLabel))]
    private string? _beard;

    /// <summary>Hair choice shown.</summary>
    public string HairLabel => Hair is null ? "class headgear" : PortraitStyles.Describe(Hair);

    /// <summary>Beard choice shown.</summary>
    public string BeardLabel => Beard is null ? "none" : PortraitStyles.Describe(Beard);

    [RelayCommand]
    private void RandomName() => Name = NameGenerator.Generate(SelectedRace.Id, Enum.Parse<Sex>(SelectedSex.Id), _main.Services.Session.Random);

    [RelayCommand]
    private void NextHair() => Hair = PortraitStyles.Cycle(PortraitStyles.Hair, Hair, 1);

    [RelayCommand]
    private void PreviousHair() => Hair = PortraitStyles.Cycle(PortraitStyles.Hair, Hair, -1);

    [RelayCommand]
    private void NextBeard() => Beard = PortraitStyles.Cycle(PortraitStyles.Beards, Beard, 1);

    [RelayCommand]
    private void PreviousBeard() => Beard = PortraitStyles.Cycle(PortraitStyles.Beards, Beard, -1);

    /// <summary>Starting hit points preview.</summary>
    public string Preview
    {
        get
        {
            if (SelectedClass is null)
            {
                return "Choose a class.";
            }
            var cls = _main.Services.Content.Class(SelectedClass.Id);
            var stats = FinalStats();
            var hp = Rulebook.StartingHp(cls, stats[Stat.Endurance]);
            return $"HP {hp}  |  Hit die d{cls.HitDie}  |  Magic: {(cls.SpellSchool is null ? "none" : $"{cls.SpellSchool} from level {cls.SpellStartLevel}")}";
        }
    }

    /// <summary>Party roster summary.</summary>
    public string PartySummary => Created.Count == 0 ? "No adventurers yet." : $"{Created.Count} / 6 adventurers";

    partial void OnSelectedRaceChanged(ChoiceViewModel value) => Recalculate();
    partial void OnSelectedAlignmentChanged(ChoiceViewModel value) => Recalculate();
    partial void OnSelectedSexChanged(ChoiceViewModel value) => OnPropertyChanged(nameof(Portrait));
    partial void OnSelectedClassChanged(ChoiceViewModel? value)
    {
        OnPropertyChanged(nameof(Preview));
        OnPropertyChanged(nameof(Portrait));
    }

    private Dictionary<Stat, int> FinalStats() => CharacterFactory.ApplyRace(_raw, _main.Services.Content.Race(SelectedRace.Id));

    private void Recalculate()
    {
        var race = _main.Services.Content.Race(SelectedRace.Id);
        var final = FinalStats();
        StatRows.Clear();
        foreach (var s in Enum.GetValues<Stat>())
        {
            var mod = race.StatModifiers.GetValueOrDefault(s);
            StatRows.Add(new StatRow(s.ToString(), _raw[s], final[s], mod == 0 ? "" : mod > 0 ? $"+{mod}" : mod.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }
        var alignment = Enum.Parse<Alignment>(SelectedAlignment.Id);
        foreach (var c in Classes)
        {
            c.IsEnabled = CharacterFactory.MeetsRequirements(final, alignment, _main.Services.Content.Class(c.Id));
        }
        if (SelectedClass is { IsEnabled: false })
        {
            SelectedClass = null;
        }
        SelectedClass ??= Classes.FirstOrDefault(c => c.IsEnabled);
        OnPropertyChanged(nameof(Portrait));
        OnPropertyChanged(nameof(Preview));
        AddCharacterCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Rolls new attributes.</summary>
    [RelayCommand]
    private void Roll()
    {
        _raw = CharacterFactory.RollStats(_main.Services.Session.Random);
        _main.Services.Audio.PlaySfx("ui");
        Recalculate();
    }

    /// <summary>Selects a class if eligible.</summary>
    /// <param name="choice">Class choice.</param>
    [RelayCommand]
    private void PickClass(ChoiceViewModel choice)
    {
        if (choice.IsEnabled)
        {
            SelectedClass = choice;
        }
    }

    private int Capacity => _recruitMode ? 1 : 6;

    private bool CanAdd() => !string.IsNullOrWhiteSpace(Name) && SelectedClass is not null && Created.Count < Capacity;

    /// <summary>Creates the character from the current choices.</summary>
    [RelayCommand(CanExecute = nameof(CanAdd))]
    private void AddCharacter()
    {
        try
        {
            var c = _main.Services.Session.Factory.Create(Name, SelectedRace.Id, SelectedClass!.Id,
                Enum.Parse<Sex>(SelectedSex.Id), Enum.Parse<Alignment>(SelectedAlignment.Id), FinalStats());
            c.Hair = Hair;
            c.Beard = Beard;
            Created.Add(c);
            Status = $"{c.Name} the {_main.Services.Content.Class(c.Class).Name} is ready.";
            _main.Services.Audio.PlaySfx("levelup");
            Name = "";
            Hair = null;
            Beard = null;
            Roll();
            OnPropertyChanged(nameof(PartySummary));
            BeginCommand.NotifyCanExecuteChanged();
            if (_recruitMode)
            {
                Begin();
            }
        }
        catch (ArgumentException ex)
        {
            Status = ex.Message;
        }
    }

    /// <summary>Removes a created character.</summary>
    /// <param name="c">Character.</param>
    [RelayCommand]
    private void Remove(Character c)
    {
        Created.Remove(c);
        OnPropertyChanged(nameof(PartySummary));
        BeginCommand.NotifyCanExecuteChanged();
        AddCharacterCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Fills the party with premade adventurers.</summary>
    [RelayCommand]
    private void QuickParty()
    {
        Created.Clear();
        foreach (var p in _main.Services.Content.Config.Premades.Take(6))
        {
            Created.Add(_main.Services.Session.Factory.CreatePremade(p));
        }
        Status = "A seasoned band of adventurers stands ready.";
        OnPropertyChanged(nameof(PartySummary));
        BeginCommand.NotifyCanExecuteChanged();
        AddCharacterCommand.NotifyCanExecuteChanged();
    }

    private bool CanBegin() => Created.Count > 0;

    /// <summary>Difficulty choices.</summary>
    public string[] DifficultyOptions { get; } = ["Easy", "Normal", "Hard"];

    /// <summary>Difficulty for the new game (remembered in settings).</summary>
    public int DifficultyIndex
    {
        get => (int)_main.Services.Settings.Difficulty;
        set
        {
            _main.Services.Settings.Difficulty = (AVAMMB1.Core.Rules.Difficulty)Math.Clamp(value, 0, 2);
            OnPropertyChanged();
            OnPropertyChanged(nameof(DifficultyText));
        }
    }

    /// <summary>What the chosen difficulty does.</summary>
    public string DifficultyText => AVAMMB1.Core.Rules.DifficultyRules.Describe(_main.Services.Settings.Difficulty);

    /// <summary>Ironman for the new game (remembered in settings).</summary>
    public bool Ironman
    {
        get => _main.Services.Settings.Ironman;
        set
        {
            _main.Services.Settings.Ironman = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Survival mode for the new game (remembered in settings).</summary>
    public bool Survival
    {
        get => _main.Services.Settings.Survival;
        set
        {
            _main.Services.Settings.Survival = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Starts the adventure (or returns to the inn when recruiting).</summary>
    [RelayCommand(CanExecute = nameof(CanBegin))]
    private void Begin()
    {
        if (_recruitMode)
        {
            foreach (var c in Created)
            {
                _main.Game?.AddMessages(_main.Services.Session.Town.Recruit(c));
            }
            _main.ReturnToGame();
            return;
        }
        _main.Services.SaveSettings();
        _main.StartNewGame(Created.ToList(), Array.Empty<Character>());
    }

    /// <summary>Goes back.</summary>
    [RelayCommand]
    private void Back()
    {
        if (_recruitMode)
        {
            _main.ReturnToGame();
        }
        else
        {
            _main.ShowTitle();
        }
    }

    /// <inheritdoc />
    public override bool HandleKey(Key key)
    {
        if (key == Key.Escape)
        {
            Back();
            return true;
        }
        return false;
    }
}
