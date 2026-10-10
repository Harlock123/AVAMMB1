using Avalonia.Controls;
using Avalonia.Threading;

namespace AVAMMB1.App.Views;

/// <summary>View for the matching view model.</summary>
public partial class RiddleView : UserControl
{
    /// <summary>Creates the view.</summary>
    public RiddleView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() => AnswerBox.Focus());
    }
}
