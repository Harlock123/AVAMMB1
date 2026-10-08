using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Threading;
using AVAMMB1.App.ViewModels;

namespace AVAMMB1.App.Views;

/// <summary>Exploration screen view.</summary>
public partial class GameView : UserControl
{
    private GameViewModel? _vm;

    /// <summary>Creates the view.</summary>
    public GameView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (_vm is not null)
            {
                _vm.Messages.CollectionChanged -= OnMessages;
            }
            _vm = DataContext as GameViewModel;
            if (_vm is not null)
            {
                _vm.Messages.CollectionChanged += OnMessages;
            }
        };
    }

    private void OnMessages(object? sender, NotifyCollectionChangedEventArgs e) =>
        Dispatcher.UIThread.Post(() => LogScroller.ScrollToEnd(), DispatcherPriority.Background);
}
