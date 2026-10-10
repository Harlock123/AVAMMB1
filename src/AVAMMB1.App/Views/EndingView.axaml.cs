using Avalonia.Controls;
using AVAMMB1.App.ViewModels;

namespace AVAMMB1.App.Views;

/// <summary>View for the matching view model.</summary>
public partial class EndingView : UserControl
{
    /// <summary>Creates the view.</summary>
    public EndingView()
    {
        InitializeComponent();
        CopyCard.Click += async (_, _) =>
        {
            if (DataContext is EndingViewModel { ShareText: { } text } vm && TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
            {
                await clipboard.SetTextAsync(text);
                vm.ShareNote = "Copied - paste it anywhere.";
            }
        };
    }
}
