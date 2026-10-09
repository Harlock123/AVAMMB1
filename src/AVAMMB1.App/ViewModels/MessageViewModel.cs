using Avalonia.Media;
using AVAMMB1.Core.Session;

namespace AVAMMB1.App.ViewModels;

/// <summary>A log line.</summary>
/// <param name="message">Source message.</param>
public sealed class MessageViewModel(GameMessage message)
{
    /// <summary>Text.</summary>
    public string Text { get; } = message.Text;

    /// <summary>Color by message kind (from the current theme).</summary>
    public IBrush Brush => message.Kind switch
    {
        MessageKind.Good => Theming.Theme.Brush("Good"),
        MessageKind.Bad => Theming.Theme.Brush("Bad"),
        MessageKind.Combat => Theming.Theme.Brush("Parchment"),
        MessageKind.Loot => Theming.Theme.Brush("Loot"),
        MessageKind.Story => Theming.Theme.Brush("Info"),
        _ => Theming.Theme.Brush("Plain"),
    };
}
