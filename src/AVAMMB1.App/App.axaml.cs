using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AVAMMB1.App.Rendering;
using AVAMMB1.App.Services;
using AVAMMB1.App.ViewModels;
using AVAMMB1.App.Views;
using AVAMMB1.Core.Content;
using AVAMMB1.Core.Dice;
using AVAMMB1.Core.Persistence;
using AVAMMB1.Core.Session;
using Microsoft.Extensions.DependencyInjection;

namespace AVAMMB1.App;

/// <summary>The Avalonia application: composes services and shows the main window.</summary>
public partial class App : Application
{
    /// <summary>Launch options (set by <see cref="Program"/>).</summary>
    public static LaunchOptions Options { get; set; } = new();

    /// <summary>The DI container.</summary>
    public static IServiceProvider Services { get; private set; } = null!;

    /// <summary>Shared texture cache (used by custom controls).</summary>
    public static TextureCache Textures => Services.GetRequiredService<TextureCache>();

    /// <inheritdoc />
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <summary>Builds the service container.</summary>
    /// <param name="options">Launch options.</param>
    /// <param name="audio">Audio implementation.</param>
    /// <param name="seed">Optional fixed random seed (deterministic automation).</param>
    public static IServiceProvider BuildServices(LaunchOptions options, IAudioService audio, int? seed = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IContentSource>(_ => ResolveContentSource(options));
        services.AddSingleton(sp => ContentDatabase.Load(sp.GetRequiredService<IContentSource>()));
        services.AddSingleton<IRandomSource>(_ => new DefaultRandomSource(seed));
        services.AddSingleton<GameSession>();
        services.AddSingleton(_ => new SaveGameService(UserDataPaths.SaveDirectory));
        services.AddSingleton(_ => new SettingsStore(UserDataPaths.SettingsFile));
        services.AddSingleton(audio);
        services.AddSingleton<TextureCache>();
        services.AddSingleton<GameServices>();
        services.AddSingleton<MainViewModel>();
        return Services = services.BuildServiceProvider();
    }

    private static IContentSource ResolveContentSource(LaunchOptions options)
    {
        if (options.ContentDir is { } dir && Directory.Exists(dir))
        {
            return new DirectoryContentSource(dir);
        }
        // A "Content" folder next to the executable overrides the embedded data (easy modding).
        var beside = Path.Combine(AppContext.BaseDirectory, "Content");
        return File.Exists(Path.Combine(beside, "game.json")) ? new DirectoryContentSource(beside) : new EmbeddedContentSource();
    }

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var audio = Options.Mute ? new NullAudioService("muted by --mute") : OpenAlAudioService.Create();
            BuildServices(Options, audio);
            var vm = Services.GetRequiredService<MainViewModel>();
            var window = new MainWindow { DataContext = vm };
            vm.QuitRequested += (_, _) => desktop.Shutdown();
            desktop.MainWindow = window;
            desktop.Exit += (_, _) => audio.Dispose();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
