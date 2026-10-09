using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using AVAMMB1.App.ViewModels;

namespace AVAMMB1.App.Views;

/// <summary>Exploration screen view.</summary>
public partial class GameView : UserControl
{
    private const double DragThreshold = 8;

    private GameViewModel? _vm;
    private int _dragFrom = -1;
    private Point _dragStart;
    private bool _dragging;
    private Control? _dragCard;
    private Control? _dropCard;

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
        // Tunnel + handled-too: the card is a Button, which would otherwise swallow the pointer.
        PartyBar.AddHandler(PointerPressedEvent, OnCardPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        PartyBar.AddHandler(PointerMovedEvent, OnCardMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
        PartyBar.AddHandler(PointerReleasedEvent, OnCardReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
        PartyBar.AddHandler(PointerCaptureLostEvent, (_, _) => EndDrag(), RoutingStrategies.Direct, handledEventsToo: true);
    }

    /// <summary>The party slot under a point on the party bar (six equal columns).</summary>
    private int SlotAt(Point p)
    {
        var count = _vm?.Party.Count ?? 0;
        var width = PartyBar.Bounds.Width / 6;
        return count == 0 || width <= 0 ? -1 : Math.Clamp((int)(p.X / width), 0, count - 1);
    }

    private void OnCardPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_vm?.CanReorder != true || !e.GetCurrentPoint(PartyBar).Properties.IsLeftButtonPressed)
        {
            return;
        }
        _dragStart = e.GetPosition(PartyBar);
        _dragFrom = SlotAt(_dragStart);
        _dragging = false;
    }

    private void OnCardMoved(object? sender, PointerEventArgs e)
    {
        if (_dragFrom < 0)
        {
            return;
        }
        var p = e.GetPosition(PartyBar);
        if (!_dragging)
        {
            if (Math.Abs(p.X - _dragStart.X) < DragThreshold && Math.Abs(p.Y - _dragStart.Y) < DragThreshold)
            {
                return;
            }
            _dragging = true;
            _dragCard = PartyBar.ContainerFromIndex(_dragFrom);
            if (_dragCard is not null)
            {
                _dragCard.ZIndex = 10;
                _dragCard.Classes.Add("dragging");
            }
            e.Pointer.Capture(PartyBar); // takes the press from the card's button, so no click follows
        }
        if (_dragCard is not null)
        {
            _dragCard.RenderTransform = new TranslateTransform(p.X - _dragStart.X, 0); // along the bar only: the row clips anything above it
        }
        var target = SlotAt(p);
        var drop = target != _dragFrom && target >= 0 ? PartyBar.ContainerFromIndex(target) : null;
        if (!ReferenceEquals(drop, _dropCard))
        {
            _dropCard?.Classes.Remove("droptarget");
            drop?.Classes.Add("droptarget");
            _dropCard = drop;
        }
        e.Handled = true;
    }

    private void OnCardReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_dragging)
        {
            _dragFrom = -1;
            return;
        }
        var from = _dragFrom;
        var to = SlotAt(e.GetPosition(PartyBar));
        EndDrag();
        e.Pointer.Capture(null);
        e.Handled = true;
        if (to >= 0 && to != from)
        {
            _vm?.MoveMember(from, to);
        }
    }

    private void EndDrag()
    {
        if (_dragCard is not null)
        {
            _dragCard.RenderTransform = null;
            _dragCard.ZIndex = 0;
            _dragCard.Classes.Remove("dragging");
        }
        _dropCard?.Classes.Remove("droptarget");
        _dragCard = _dropCard = null;
        _dragFrom = -1;
        _dragging = false;
    }

    private void OnMessages(object? sender, NotifyCollectionChangedEventArgs e) =>
        Dispatcher.UIThread.Post(() => LogScroller.ScrollToEnd(), DispatcherPriority.Background);
}
