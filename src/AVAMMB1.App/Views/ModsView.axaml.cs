using Avalonia.Controls;
using Avalonia.Platform.Storage;
using AVAMMB1.App.ViewModels;

namespace AVAMMB1.App.Views;

/// <summary>View for the matching view model.</summary>
public partial class ModsView : UserControl
{
    /// <summary>Creates the view.</summary>
    public ModsView()
    {
        InitializeComponent();
        OpenFolder.Click += async (_, _) =>
        {
            if (DataContext is ModsViewModel vm && TopLevel.GetTopLevel(this)?.Launcher is { } launcher)
            {
                await launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(vm.EnsureFolder()));
            }
        };
    }
}
