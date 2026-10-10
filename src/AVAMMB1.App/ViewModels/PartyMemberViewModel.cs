using Avalonia.Media.Imaging;
using AVAMMB1.App.Services;
using AVAMMB1.Core.Characters;
using AVAMMB1.Core.Rules;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AVAMMB1.App.ViewModels;

/// <summary>Display wrapper around a <see cref="Character"/>.</summary>
public sealed partial class PartyMemberViewModel : ObservableObject
{
    private readonly GameServices _services;

    /// <summary>Creates the wrapper.</summary>
    /// <param name="services">Services.</param>
    /// <param name="character">Character.</param>
    /// <param name="index">Party index.</param>
    public PartyMemberViewModel(GameServices services, Character character, int index)
    {
        _services = services;
        Character = character;
        Index = index;
    }

    /// <summary>The character.</summary>
    public Character Character { get; }
    /// <summary>Party index.</summary>
    public int Index { get; }
    /// <summary>1-based slot label.</summary>
    public string Slot => (Index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
    /// <summary>Name.</summary>
    public string Name => Character.Name;
    /// <summary>"Knight 3" style label.</summary>
    public string ClassLevel => $"{_services.Content.Race(Character.Race).Name} {_services.Content.Class(Character.Class).Name} {Character.Level}";
    /// <summary>Hit points text.</summary>
    public string HpText => $"{Character.Hp}/{Character.MaxHp}";
    /// <summary>Spell points text.</summary>
    public string SpText => Character.MaxSp > 0 ? $"{Character.Sp}/{Character.MaxSp}" : "-";
    /// <summary>Armor class.</summary>
    public int ArmorClass => _services.Session.Rules.ArmorClass(Character);
    /// <summary>Most severe condition.</summary>
    public string Status => Character.StatusText;
    /// <summary>Whether healthy.</summary>
    public bool IsOk => Character.Conditions == Condition.None;
    /// <summary>HP fraction 0..1.</summary>
    /// <summary>Badly hurt (a quarter of HP or less, still standing): shown as text too, not only colour.</summary>
    public bool LowHp => Character.IsAlive && Character.Hp > 0 && Character.Hp * 4 <= Character.MaxHp;

    /// <summary>Hit point fraction.</summary>
    public double HpFraction => Character.MaxHp == 0 ? 0 : Math.Clamp((double)Character.Hp / Character.MaxHp, 0, 1);
    /// <summary>SP fraction 0..1.</summary>
    public double SpFraction => Character.MaxSp == 0 ? 0 : Math.Clamp((double)Character.Sp / Character.MaxSp, 0, 1);
    /// <summary>Food carried.</summary>
    public int Food => Character.Food;
    /// <summary>Whether the character may train.</summary>
    public bool CanLevel => _services.Session.Rules.CanLevelUp(Character);
    /// <summary>Portrait.</summary>
    public Bitmap? Portrait => _services.Portrait(Character.Race, Character.Sex, Character.Class);

    /// <summary>Highlighted as the acting combatant.</summary>
    [ObservableProperty]
    private bool _isActive;

    /// <summary>Briefly true after taking damage (drives the hurt flash).</summary>
    [ObservableProperty]
    private bool _isHurt;

    /// <summary>Re-reads every property.</summary>
    public void Refresh() => OnPropertyChanged(string.Empty);
}
