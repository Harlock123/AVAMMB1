using Avalonia.Controls;
using AVAMMB1.App.ViewModels;

namespace AVAMMB1.App.Views;

/// <summary>View for the matching view model.</summary>
public partial class MapEditorView : UserControl
{
    /// <summary>Creates the view.</summary>
    public MapEditorView()
    {
        InitializeComponent();
        Canvas.CellClicked += (x, y, side, drag) => (DataContext as MapEditorViewModel)?.Click(x, y, side, drag);
    }
}
