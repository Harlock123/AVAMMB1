using Avalonia.Input;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.Input;

namespace AVAMMB1.App.ViewModels;

/// <summary>Title screen.</summary>
public sealed partial class TitleViewModel : ViewModelBase
{
    private readonly MainViewModel _main;

    /// <summary>Creates the title screen.</summary>
    /// <param name="main">Root view model.</param>
    public TitleViewModel(MainViewModel main)
    {
        _main = main;
        main.Services.Audio.PlayMusic("title");
        main.Services.Audio.PlayAmbience(null);
        var recent = main.Services.Saves.MostRecentSlot();
        CanContinue = recent is not null;
        _continueSlot = recent ?? 0;
        var t = main.Services.Textures;
        Showcase = new[] { "Monsters/vault_warden", "Monsters/crypt_lich", "Monsters/minotaur", "Monsters/troll", "Monsters/kobold_chief" }
            .Select(t.Bitmap).Where(b => b is not null).Cast<Bitmap>().ToList();
    }

    private readonly int _continueSlot;

    /// <summary>Whether a save exists to continue from.</summary>
    public bool CanContinue { get; }

    /// <summary>Monster sprites displayed on the title screen.</summary>
    public IReadOnlyList<Bitmap> Showcase { get; }

    /// <summary>Version string.</summary>
    public string Version => "v" + (typeof(TitleViewModel).Assembly.GetName().Version?.ToString(3) ?? "1.0.0");

    /// <summary>Last error.</summary>
    public string? Error { get; private set; }

    [RelayCommand]
    private void NewGame()
    {
        _main.Services.Audio.PlaySfx("ui");
        _main.ShowNewGame();
    }

    [RelayCommand]
    private void Continue()
    {
        Error = _main.LoadSlot(_continueSlot);
        OnPropertyChanged(nameof(Error));
    }

    [RelayCommand]
    private void Load() => _main.ShowLoad();

    [RelayCommand]
    private void Help() => _main.ShowHelp();

    [RelayCommand]
    private void Mods() => _main.ShowMods();

    [RelayCommand]
    private void WhatsNew() => _main.ShowAllNotes();

    [RelayCommand]
    private void Settings() => _main.ShowSettings(this);

    [RelayCommand]
    private void Credits() => _main.ShowCredits();

    [RelayCommand]
    private void HallOfFame() => _main.ShowHallOfFame();

    [RelayCommand]
    private void Quit() => _main.Quit();

    /// <inheritdoc />
    public override bool HandleKey(Key key)
    {
        switch (key)
        {
            case Key.N: NewGame(); return true;
            case Key.C when CanContinue: Continue(); return true;
            case Key.L: Load(); return true;
            case Key.S: Settings(); return true;
            case Key.F1 or Key.H: Help(); return true;
            default: return false;
        }
    }
}
