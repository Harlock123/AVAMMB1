using Avalonia;
using AVAMMB1.App.Services;

namespace AVAMMB1.App;

/// <summary>Entry point.</summary>
public static class Program
{
    /// <summary>
    /// Main. Supports <c>--screenshot DIR</c>, <c>--smoke-test</c>, <c>--content DIR</c>, <c>--mods DIR</c>,
    /// <c>--check-content</c> and <c>--mute</c>.
    /// </summary>
    /// <param name="args">Command line.</param>
    [STAThread]
    public static int Main(string[] args)
    {
        var options = LaunchOptions.Parse(args);
        App.Options = options;
        if (options.CheckContent)
        {
            return ContentCheck.Run(options);
        }
        if (options.ScreenshotDir is not null || options.SmokeTest)
        {
            return HeadlessRunner.Run(options);
        }
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    /// <summary>Avalonia configuration (also used by the designer).</summary>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
