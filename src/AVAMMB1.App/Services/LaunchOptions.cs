namespace AVAMMB1.App.Services;

/// <summary>Command line options.</summary>
public sealed class LaunchOptions
{
    /// <summary>Directory to write automated screenshots to (headless mode).</summary>
    public string? ScreenshotDir { get; private set; }
    /// <summary>Directory to load content JSON from instead of the embedded copy.</summary>
    public string? ContentDir { get; private set; }
    /// <summary>Run a headless self-test and exit.</summary>
    public bool SmokeTest { get; private set; }
    /// <summary>Disable audio.</summary>
    public bool Mute { get; private set; }

    /// <summary>Parses arguments.</summary>
    /// <param name="args">Command line.</param>
    public static LaunchOptions Parse(string[] args)
    {
        var o = new LaunchOptions();
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--screenshot" or "--screenshots":
                    o.ScreenshotDir = i + 1 < args.Length ? args[++i] : "screenshots";
                    break;
                case "--content":
                    o.ContentDir = i + 1 < args.Length ? args[++i] : null;
                    break;
                case "--smoke-test":
                    o.SmokeTest = true;
                    break;
                case "--mute":
                    o.Mute = true;
                    break;
            }
        }
        return o;
    }
}
