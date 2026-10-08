using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using AVAMMB1.App.ViewModels;

namespace AVAMMB1.App.Views;

/// <summary>The game window. Routes keyboard input to the current screen.</summary>
public partial class MainWindow : Window
{
    /// <summary>Creates the window.</summary>
    public MainWindow()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDownTunnel, RoutingStrategies.Tunnel);
        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainViewModel vm)
            {
                vm.FullscreenChanged += (_, _) => ApplyFullscreen(vm);
                ApplyFullscreen(vm);
            }
        };
    }

    private void ApplyFullscreen(MainViewModel vm) =>
        WindowState = vm.Services.Settings.Fullscreen ? WindowState.FullScreen : WindowState.Normal;

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
}
