using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using AVAMMB1.App.ViewModels;

namespace AVAMMB1.App.Views;

/// <summary>View for the matching view model.</summary>
public partial class AutomapView : UserControl
{
    /// <summary>Creates the view.</summary>
    public AutomapView()
    {
        InitializeComponent();
        Map.CellClicked += (_, cell) => (DataContext as AutomapViewModel)?.Select(cell);
        NoteBox.KeyDown += (_, e) =>
        {
            // Enter saves the note; Escape leaves the box without closing the map.
            if (e.Key == Key.Enter && DataContext is AutomapViewModel vm)
            {
                vm.SaveNoteCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                Map.Focus();
                e.Handled = true;
            }
        };
    }

    /// <inheritdoc />
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (DataContext is AutomapViewModel { EditNote: true })
        {
            NoteBox.Focus();
        }
    }
}
