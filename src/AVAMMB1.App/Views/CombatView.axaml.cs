using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Threading;
using AVAMMB1.App.ViewModels;

namespace AVAMMB1.App.Views;

/// <summary>Combat overlay view.</summary>
public partial class CombatView : UserControl
{
    /// <summary>Creates the view.</summary>
    public CombatView()
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
