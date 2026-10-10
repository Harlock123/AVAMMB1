using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using AVAMMB1.App.Services;
using AVAMMB1.App.ViewModels;
using AVAMMB1.Core.Input;

namespace AVAMMB1.App.Views;

/// <summary>
/// The game window. Routes keyboard and controller input to the current screen and scales the
/// interface (designed for 1280x800) to fit the window.
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>The resolution the screens are laid out for.</summary>
    public static readonly Size DesignSize = new(1280, 800);

    private readonly Viewbox _scaler = new() { Stretch = Stretch.Uniform };
    private readonly LayoutTransformControl _zoomer = new();
    private GamepadService? _gamepad;

    /// <summary>Creates the window.</summary>
    public MainWindow()
    {
        InitializeComponent();
        Background = Brushes.Black;
        AddHandler(KeyDownEvent, OnKeyDownTunnel, RoutingStrategies.Tunnel);
        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainViewModel vm)
            {
                vm.FullscreenChanged += (_, _) => ApplyFullscreen(vm);
                vm.LayoutChanged += (_, _) => ApplyLayout(vm);
                ApplyFullscreen(vm);
                ApplyLayout(vm);
            }
        };
    }

    /// <summary>Starts listening to a controller service.</summary>
    /// <param name="gamepad">The service.</param>
    public void AttachGamepad(GamepadService gamepad)
    {
        _gamepad = gamepad;
        gamepad.ButtonPressed += OnGamepad;
    }

    private void ApplyFullscreen(MainViewModel vm) =>
        WindowState = vm.Services.Settings.Fullscreen ? WindowState.FullScreen : WindowState.Normal;

    /// <summary>
    /// Fit-to-window renders the 1280x800 layout scaled uniformly; otherwise it fills the window, enlarged
    /// by the interface zoom (for big or high-resolution screens).
    /// </summary>
    private void ApplyLayout(MainViewModel vm)
    {
        var settings = vm.Services.Settings;
        var zoom = Math.Clamp(settings.InterfaceZoom, 100, 150) / 100.0;
        Content = null;
        _scaler.Child = null;
        _zoomer.Child = null;
        if (settings.FitToWindow)
        {
            Screen.Width = DesignSize.Width;
            Screen.Height = DesignSize.Height;
            _scaler.Child = Screen;
            Content = _scaler;
            MinWidth = 640;
            MinHeight = 400;
            return;
        }
        Screen.Width = double.NaN;
        Screen.Height = double.NaN;
        if (zoom > 1.001)
        {
            _zoomer.LayoutTransform = new ScaleTransform(zoom, zoom);
            _zoomer.Child = Screen;
            Content = _zoomer;
        }
        else
        {
            Content = Screen;
        }
        MinWidth = 1024 * zoom;
        MinHeight = 700 * zoom;
    }

    private void OnKeyDownTunnel(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }
        if (e.Key == Key.F11 || (e.Key == Key.Enter && e.KeyModifiers.HasFlag(KeyModifiers.Alt)))
        {
            vm.Services.Settings.Fullscreen = !vm.Services.Settings.Fullscreen;
            vm.Services.SaveSettings();
            ApplyFullscreen(vm);
            e.Handled = true;
            return;
        }
        if (_keyboard is { } keyboard && e.Key == Key.Escape)
        {
            keyboard.Cancel();
            e.Handled = true;
            return;
        }
        // Let text boxes receive typing (names), except for Escape.
        if (e.Source is TextBox && e.Key != Key.Escape)
        {
            return;
        }
        if (vm.HandleKey(e.Key))
        {
            e.Handled = true;
        }
    }

    // ------------------------------------------------------------------ controller input

    private void OnGamepad(GamepadButton button)
    {
        if (DataContext is not MainViewModel vm || !vm.Services.Settings.GamepadEnabled || !IsActive)
        {
            return;
        }
        if (vm.CurrentScreen is SettingsViewModel { IsCapturingPad: true } settings)
        {
            settings.CapturePad(button);
            return;
        }
        var context = vm.CurrentScreen is GameViewModel g ? g.GamepadContext : GamepadContext.Menu;
        var command = GamepadMapping.Map(context, button, vm.Services.Settings.GamepadBindings);
        if (command.Action is { } action && vm.CurrentScreen is GameViewModel game)
        {
            game.Perform(action);
        }
        else if (command.Combat is { } combat && vm.CurrentScreen is GameViewModel { Combat: { } fight })
        {
            fight.Gamepad(combat);
        }
        else if (command.Menu is { } menu)
        {
            Navigate(vm, menu);
        }
    }

    private Controls.OnScreenKeyboard? _keyboard;
    private Canvas? _keyboardLayer;

    /// <summary>Whether the on-screen keyboard is open.</summary>
    public bool KeyboardOpen => _keyboard is not null;

    /// <summary>Opens the on-screen keyboard for a text box (A on a focused text box with a controller).</summary>
    /// <param name="box">The text box.</param>
    public void OpenKeyboard(TextBox box)
    {
        CloseKeyboard();
        if (Avalonia.Controls.Primitives.OverlayLayer.GetOverlayLayer(this) is not { } layer)
        {
            return;
        }
        var keyboard = new Controls.OnScreenKeyboard(box);
        _keyboardLayer = new Canvas();
        _keyboardLayer.Children.Add(keyboard);
        layer.Children.Add(_keyboardLayer);
        keyboard.Measure(Size.Infinity);
        var below = box.TranslatePoint(new Point(0, box.Bounds.Height + 6), this) ?? new Point(20, 20);
        var x = Math.Clamp(below.X, 8, Math.Max(8, Bounds.Width - keyboard.DesiredSize.Width - 8));
        var y = below.Y + keyboard.DesiredSize.Height > Bounds.Height - 8
            ? Math.Max(8, (box.TranslatePoint(new Point(0, 0), this)?.Y ?? 0) - keyboard.DesiredSize.Height - 6)
            : below.Y;
        Canvas.SetLeft(keyboard, x);
        Canvas.SetTop(keyboard, y);
        keyboard.Closed += (_, _) => CloseKeyboard();
        _keyboard = keyboard;
        // Focus the first key once the keyboard has been laid out where it belongs (not at the corner).
        Avalonia.Threading.Dispatcher.UIThread.Post(() => keyboard.FirstKey.Focus(NavigationMethod.Directional), Avalonia.Threading.DispatcherPriority.Background);
    }

    /// <summary>Closes the on-screen keyboard, if open.</summary>
    public void CloseKeyboard()
    {
        if (_keyboardLayer is not null && Avalonia.Controls.Primitives.OverlayLayer.GetOverlayLayer(this) is { } layer)
        {
            layer.Children.Remove(_keyboardLayer);
        }
        _keyboard = null;
        _keyboardLayer = null;
    }

    /// <summary>The part of the window the D-pad should move around in: the top dialog, the combat panel, or the whole screen.</summary>
    private Visual ActiveRoot(MainViewModel vm)
    {
        if (_keyboard is not null)
        {
            return _keyboard;
        }
        if (vm.CurrentScreen is GameViewModel game)
        {
            var view = this.GetVisualDescendants().OfType<GameView>().FirstOrDefault();
            if (view is not null && game.Overlay is not null)
            {
                return view.FindControl<Border>("OverlayHost") ?? (Visual)view;
            }
            if (view is not null && game.Combat is not null)
            {
                return view.GetVisualDescendants().OfType<CombatPanelView>().FirstOrDefault() ?? (Visual)view;
            }
        }
        return Screen;
    }

    private static bool IsNavigable(Control c) =>
        c is Button or CheckBox or ListBoxItem or Slider or ComboBox or NumericUpDown or TextBox
        && c.IsEffectivelyVisible && c.IsEffectivelyEnabled && c.Focusable && c.Bounds.Width > 0;

    private void Navigate(MainViewModel vm, MenuCommand command)
    {
        var root = ActiveRoot(vm);
        var candidates = root.GetVisualDescendants().OfType<Control>().Where(IsNavigable).ToList();
        var focused = FocusManager?.GetFocusedElement() as Control;
        if (focused is not null && !candidates.Contains(focused))
        {
            focused = null;
        }

        if (_keyboard is { } keyboard && command == MenuCommand.Back)
        {
            keyboard.Cancel();
            return;
        }
        if (focused is TextBox box && command == MenuCommand.Activate && _keyboard is null)
        {
            OpenKeyboard(box);
            return;
        }
        switch (command)
        {
            case MenuCommand.Back:
                vm.HandleKey(Key.Escape);
                return;
            case MenuCommand.Activate:
                if (focused is null || !Activate(focused))
                {
                    vm.HandleKey(Key.Enter); // e.g. "Continue" on story text
                }
                return;
        }

        if (focused is Slider slider && command is MenuCommand.Left or MenuCommand.Right)
        {
            slider.Value = Math.Clamp(slider.Value + (command == MenuCommand.Right ? 5 : -5), slider.Minimum, slider.Maximum);
            return;
        }
        if (focused is NumericUpDown number && command is MenuCommand.Left or MenuCommand.Right)
        {
            number.Value = Math.Max(number.Minimum, (number.Value ?? 0) + (command == MenuCommand.Right ? number.Increment : -number.Increment));
            return;
        }
        var next = focused is null ? candidates.FirstOrDefault() : Nearest(focused, candidates, command);
        next?.Focus(NavigationMethod.Directional);
        next?.BringIntoView();
    }

    /// <summary>Spatial navigation: the closest control whose center lies in the pressed direction.</summary>
    private Control? Nearest(Control from, List<Control> candidates, MenuCommand direction)
    {
        Point? Center(Control c) => c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), this);
        if (Center(from) is not { } origin)
        {
            return null;
        }
        Control? best = null;
        var bestScore = double.MaxValue;
        foreach (var c in candidates)
        {
            if (ReferenceEquals(c, from) || Center(c) is not { } p)
            {
                continue;
            }
            var dx = p.X - origin.X;
            var dy = p.Y - origin.Y;
            var (along, across) = direction switch
            {
                MenuCommand.Up => (-dy, dx),
                MenuCommand.Down => (dy, dx),
                MenuCommand.Left => (-dx, dy),
                _ => (dx, dy),
            };
            if (along <= 1)
            {
                continue; // not in that direction
            }
            var score = along + Math.Abs(across) * 2.5; // prefer controls straight ahead
            if (score < bestScore)
            {
                bestScore = score;
                best = c;
            }
        }
        return best;
    }

    private static bool Activate(Control c)
    {
        switch (c)
        {
            case Button { Command: { } cmd } b when cmd.CanExecute(b.CommandParameter):
                cmd.Execute(b.CommandParameter);
                return true;
            case CheckBox box:
                box.IsChecked = box.IsChecked != true;
                return true;
            case Button b:
                b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                return true;
            case ListBoxItem item:
                item.IsSelected = true;
                return true;
            case ComboBox combo:
                combo.IsDropDownOpen = !combo.IsDropDownOpen;
                return true;
            default:
                return false;
        }
    }
}
