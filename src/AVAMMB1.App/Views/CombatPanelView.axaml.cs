using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Threading;
using AVAMMB1.App.ViewModels;

namespace AVAMMB1.App.Views;

/// <summary>Combat controls and battle log shown in the side panel.</summary>
public partial class CombatPanelView : UserControl
{
    /// <summary>Creates the view.</summary>
    public CombatPanelView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is CombatViewModel vm)
            {
                vm.Log.CollectionChanged += OnLog;
            }
        };
    }

    private void OnLog(object? sender, NotifyCollectionChangedEventArgs e) =>
        Dispatcher.UIThread.Post(() => LogScroller.ScrollToEnd(), DispatcherPriority.Background);
}
