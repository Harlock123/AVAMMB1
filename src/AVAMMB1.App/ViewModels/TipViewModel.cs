using CommunityToolkit.Mvvm.Input;

namespace AVAMMB1.App.ViewModels;

/// <summary>A tip for new players, shown over the 3D view.</summary>
/// <param name="title">Heading.</param>
/// <param name="text">Text (keys already filled in).</param>
/// <param name="dismiss">Closes it.</param>
/// <param name="turnOff">Switches tips off.</param>
public sealed partial class TipViewModel(string title, string text, Action dismiss, Action turnOff) : ViewModelBase
{
    /// <summary>Heading.</summary>
    public string Title { get; } = title;

    /// <summary>Text.</summary>
    public string Text { get; } = text;

    [RelayCommand]
    private void Dismiss() => dismiss();

    [RelayCommand]
    private void TurnOff() => turnOff();
}
