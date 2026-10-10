using Avalonia.Input;
using AVAMMB1.Core.Content;
using AVAMMB1.Core.Session;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AVAMMB1.App.ViewModels;

/// <summary>A riddle door: type the answer.</summary>
public sealed partial class RiddleViewModel : ViewModelBase
{
    private readonly GameViewModel _game;
    private readonly MapEventDef _ev;

    /// <summary>Creates the dialog.</summary>
    /// <param name="game">Owner.</param>
    /// <param name="ev">Riddle event.</param>
    public RiddleViewModel(GameViewModel game, MapEventDef ev)
    {
        _game = game;
        _ev = ev;
        Title = ev.Name ?? "A Riddle";
        Question = ev.Text ?? "";
        game.Services.Audio.PlaySfx("book");
    }

    /// <summary>Title.</summary>
    public string Title { get; }
    /// <summary>The question.</summary>
    public string Question { get; }

    /// <summary>The answer being typed.</summary>
    [ObservableProperty]
    private string _answer = "";

    /// <summary>What happened.</summary>
    [ObservableProperty]
    private string _feedback = "Type your answer and press Enter. (Esc to leave it for now.)";

    [RelayCommand]
    private void Submit()
    {
        var (correct, log) = _game.Services.Session.AnswerRiddle(_ev, Answer);
        _game.AddMessages(log);
        _game.Refresh();
        if (correct)
        {
            _game.CloseOverlay();
            return;
        }
        Feedback = string.Join(" ", log.Select(m => m.Text));
        Answer = "";
    }

    [RelayCommand]
    private void Leave() => _game.CloseOverlay();

    /// <inheritdoc />
    public override bool HandleKey(Key key)
    {
        if (key == Key.Escape)
        {
            Leave();
            return true;
        }
        return false;
    }
}

/// <summary>A choice between options.</summary>
/// <param name="Index">Option index.</param>
/// <param name="Label">Button text.</param>
public sealed record DecisionOption(int Index, string Label);

/// <summary>A decision with consequences.</summary>
public sealed partial class DecisionViewModel : ViewModelBase
{
    private readonly GameViewModel _game;
    private readonly MapEventDef _ev;

    /// <summary>Creates the dialog.</summary>
    /// <param name="game">Owner.</param>
    /// <param name="ev">Choice event.</param>
    public DecisionViewModel(GameViewModel game, MapEventDef ev)
    {
        _game = game;
        _ev = ev;
        Title = ev.Name ?? "A Choice";
        Text = ev.Text ?? "";
        Options = ev.Options.Select((o, i) => new DecisionOption(i, $"{i + 1}. {o.Label}")).ToList();
        game.Services.Audio.PlaySfx("book");
    }

    /// <summary>Title.</summary>
    public string Title { get; }
    /// <summary>The situation.</summary>
    public string Text { get; }
    /// <summary>The options.</summary>
    public IReadOnlyList<DecisionOption> Options { get; }

    [RelayCommand]
    private void Choose(DecisionOption option)
    {
        var log = _game.Services.Session.Choose(_ev, option.Index);
        _game.CloseOverlay();
        _game.AddMessages(log);
        _game.Refresh();
    }

    [RelayCommand]
    private void Later() => _game.CloseOverlay();

    /// <inheritdoc />
    public override bool HandleKey(Key key)
    {
        if (key >= Key.D1 && key - Key.D1 < Options.Count)
        {
            Choose(Options[key - Key.D1]);
            return true;
        }
        if (key == Key.Escape)
        {
            Later();
            return true;
        }
        return false;
    }
}

/// <summary>A destination on the travel map.</summary>
/// <param name="MapId">Town.</param>
/// <param name="Label">"1. Saltreach".</param>
/// <param name="Details">"about 5 hours - through the Greenvale Wilds - fare 200".</param>
public sealed record TravelRow(string MapId, string Label, string Details);

/// <summary>The travel map: known towns and what the road to each takes.</summary>
public sealed partial class TravelViewModel : ViewModelBase
{
    private readonly GameViewModel _game;

    /// <summary>Creates the dialog.</summary>
    /// <param name="game">Owner.</param>
    public TravelViewModel(GameViewModel game)
    {
        _game = game;
        Rows = game.Services.Session.TravelOptions().Select((o, i) => new TravelRow(o.MapId, $"{i + 1}. {o.Name}",
            string.Join(" - ", new[]
            {
                o.Duration,
                o.Via.Count > 0 ? "through " + string.Join(", ", o.Via) : null,
                o.Fare > 0 ? $"fare {o.Fare} gold" : null,
            }.Where(x => x is not null)))).ToList();
        Note = Rows.Count == 0
            ? "There is nowhere you know the way to yet. Towns you have visited appear here."
            : "Time passes as you travel, and the open country may hold an ambush. Choose a destination.";
    }

    /// <summary>Destinations.</summary>
    public IReadOnlyList<TravelRow> Rows { get; }
    /// <summary>Explanation.</summary>
    public string Note { get; }

    [RelayCommand]
    private void Go(TravelRow row) => _game.TravelTo(row.MapId);

    [RelayCommand]
    private void Close() => _game.CloseOverlay();

    /// <inheritdoc />
    public override bool HandleKey(Key key)
    {
        if (key >= Key.D1 && key - Key.D1 < Rows.Count)
        {
            Go(Rows[key - Key.D1]);
            return true;
        }
        if (key is Key.Escape or Key.T)
        {
            Close();
            return true;
        }
        return false;
    }
}
