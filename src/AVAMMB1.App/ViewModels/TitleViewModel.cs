using Avalonia.Input;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
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
        RefreshUpdate();
        DailyRecords = AVAMMB1.Core.Session.DailyCard.Records(main.Services.HallOfFameStore.Load(), MainViewModel.Today);
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

    /// <summary>"Version X is available", when the last check found a newer release.</summary>
    [ObservableProperty]
    private string? _updateText;

    /// <summary>Whether a newer release is known.</summary>
    public bool HasUpdate => UpdateText is not null;

    partial void OnUpdateTextChanged(string? value) => OnPropertyChanged(nameof(HasUpdate));

    /// <summary>Re-reads what the last update check found.</summary>
    public void RefreshUpdate()
    {
        var s = _main.Services.Settings;
        UpdateText = s.CheckForUpdates && AVAMMB1.Core.Info.UpdateCheck.Offer(MainViewModel.GameVersion, s.LatestRelease) is { } v
            ? AVAMMB1.Core.Info.Loc.F("Version {0} is out (you have {1})", v, MainViewModel.GameVersion)
            : null;
    }

    /// <summary>The release download page.</summary>
    public static string ReleasesPage => AVAMMB1.Core.Info.UpdateCheck.ReleasesPage;

    /// <summary>Daily challenge records (today, yesterday, best), or null before the first.</summary>
    public string? DailyRecords { get; }

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
    private void DailyChallenge() => _main.StartDailyChallenge();

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
