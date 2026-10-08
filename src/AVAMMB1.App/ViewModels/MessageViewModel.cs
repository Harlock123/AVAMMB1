using Avalonia.Media;
using AVAMMB1.Core.Session;

namespace AVAMMB1.App.ViewModels;

/// <summary>A log line.</summary>
/// <param name="message">Source message.</param>
public sealed class MessageViewModel(GameMessage message)
{
    /// <summary>Text.</summary>
    public string Text { get; } = message.Text;

    /// <summary>Color by message kind.</summary>
    public IBrush Brush { get; } = message.Kind switch
    {
        MessageKind.Good => new SolidColorBrush(Color.Parse("#8fe08f")),
        MessageKind.Bad => new SolidColorBrush(Color.Parse("#ff8a7a")),
        MessageKind.Combat => new SolidColorBrush(Color.Parse("#e8d9b0")),
        MessageKind.Loot => new SolidColorBrush(Color.Parse("#ffd75e")),
        MessageKind.Story => new SolidColorBrush(Color.Parse("#9fd0ff")),
        _ => new SolidColorBrush(Color.Parse("#c8c8d0")),
    };
}
