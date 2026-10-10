using Avalonia.Controls;
using AVAMMB1.App.ViewModels;

namespace AVAMMB1.App.Views;

/// <summary>Title screen view.</summary>
public partial class TitleView : UserControl
{
    /// <summary>Creates the view.</summary>
    public TitleView()
    {
        InitializeComponent();
        GetUpdate.Click += async (_, _) =>
        {
            if (TopLevel.GetTopLevel(this)?.Launcher is { } launcher)
            {
                await launcher.LaunchUriAsync(new Uri(TitleViewModel.ReleasesPage));
            }
        };
    }
}
