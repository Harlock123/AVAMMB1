using Avalonia.Controls;
using Avalonia.Controls.Templates;
using AVAMMB1.App.ViewModels;
using AVAMMB1.App.Views;

namespace AVAMMB1.App;

/// <summary>Maps view models to views without reflection (trim friendly).</summary>
public sealed class ViewLocator : IDataTemplate
{
    /// <inheritdoc />
    public Control? Build(object? param) => param switch
    {
        TitleViewModel => new TitleView(),
        PartyCreationViewModel => new PartyCreationView(),
        GameViewModel => new GameView(),
        StoryViewModel => new StoryView(),
        RiddleViewModel => new RiddleView(),
        DecisionViewModel => new DecisionView(),
        ShopViewModel => new ShopView(),
        InnViewModel => new InnView(),
        TempleViewModel => new TempleView(),
        TavernViewModel => new TavernView(),
        TrainingViewModel => new TrainingView(),
        AcademyViewModel => new AcademyView(),
        CharacterSheetViewModel => new CharacterSheetView(),
        SpellCastViewModel => new SpellCastView(),
        AutomapViewModel => new AutomapView(),
        JournalViewModel => new JournalView(),
        WhatsNewViewModel => new WhatsNewView(),
        HelpViewModel => new HelpView(),
        ModsViewModel => new ModsView(),
        GameMenuViewModel => new GameMenuView(),
        SaveLoadViewModel => new SaveLoadView(),
        SettingsViewModel => new SettingsView(),
        CreditsViewModel => new CreditsView(),
        EndingViewModel => new EndingView(),
        _ => new TextBlock { Text = "No view for " + param?.GetType().Name },
    };

    /// <inheritdoc />
    public bool Match(object? data) => data is ViewModelBase;
}
