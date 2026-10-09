using Avalonia;
using Avalonia.Headless;
using Avalonia.Threading;
using AVAMMB1.App.ViewModels;
using AVAMMB1.App.Views;
using AVAMMB1.Core.Combat;
using AVAMMB1.Core.Content;
using AVAMMB1.Core.Persistence;
using AVAMMB1.Core.Rules;
using AVAMMB1.Core.Session;
using Microsoft.Extensions.DependencyInjection;

namespace AVAMMB1.App.Services;

/// <summary>
/// Runs the real game UI on Avalonia's headless platform (with Skia rendering) to capture genuine
/// screenshots (<c>--screenshot DIR</c>) or to self-test a build (<c>--smoke-test</c>).
/// The party is driven through the same commands a player would use.
/// </summary>
public static class HeadlessRunner
{
    private static MainWindow _window = null!;

    /// <summary>Runs the headless mode.</summary>
    /// <param name="options">Options.</param>
    /// <returns>Process exit code.</returns>
    public static int Run(LaunchOptions options)
    {
        // Keep automation away from the player's real saves and settings.
        var temp = Path.Combine(Path.GetTempPath(), "avammb1-headless-" + Environment.ProcessId);
        Environment.SetEnvironmentVariable(UserDataPaths.OverrideVariable, temp);
        try
        {
            AppBuilder.Configure<App>()
                .UseSkia()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
                .WithInterFont()
                .SetupWithoutStarting();

            App.BuildServices(options, new NullAudioService("headless mode"), seed: 1986);
            var vm = App.Services.GetRequiredService<MainViewModel>();
            // Screenshots must show settled frames, not the middle of a step or a monster lunge.
            vm.Services.Settings.SmoothMovement = false;
            vm.Services.Settings.AnimateMonsters = false;
            _window = new MainWindow { DataContext = vm, Width = 1280, Height = 800 };
            _window.Show();
            Pump();

            if (options.SmokeTest)
            {
                SmokeTest(vm);
                // The bundled OpenAL must load (a missing library is a packaging bug); having no
                // sound device (CI machines) is fine.
                if (!OpenAlAudioService.TryLoadLibrary(out _, out var lib) || !lib.StartsWith("bundled", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Bundled OpenAL library was not loaded: " + lib);
                }
                using (var audio = OpenAlAudioService.Create())
                {
                    var detail = audio is OpenAlAudioService al ? $"{al.Status}, device open, test buffer {al.Preload("ui")}" : $"{lib} loaded; {audio.Status}";
                    Console.WriteLine("Audio: " + detail);
                }
                Console.WriteLine("SMOKE TEST OK");
                return 0;
            }
            Screenshots(vm, options.ScreenshotDir!);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Headless run failed: " + ex);
            return 1;
        }
        finally
        {
            try
            {
                Directory.Delete(temp, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    private static void Pump()
    {
        for (var i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        Dispatcher.UIThread.RunJobs();
    }

    private static void Capture(string dir, string name)
    {
        Pump();
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, name + ".png");
        // Render into a bitmap we own (the headless renderer's own frame buffer can be recycled
        // underneath us, which crashed Skia's PNG encoder intermittently).
        var size = new PixelSize((int)_window.ClientSize.Width, (int)_window.ClientSize.Height);
        using (var rtb = new Avalonia.Media.Imaging.RenderTargetBitmap(size, new Vector(96, 96)))
        {
            rtb.Render(_window);
            rtb.Save(path);
        }
        Console.WriteLine("captured " + path);
    }

    private static GameViewModel StartQuickParty(MainViewModel vm)
    {
        vm.ShowNewGame();
        var creation = (PartyCreationViewModel)vm.CurrentScreen;
        creation.QuickPartyCommand.Execute(null);
        creation.BeginCommand.Execute(null);
        return vm.Game ?? throw new InvalidOperationException("Game did not start.");
    }

    private static void SmokeTest(MainViewModel vm)
    {
        var game = StartQuickParty(vm);
        game.CloseOverlay();
        game.HandleKey(Avalonia.Input.Key.Up);
        Pump();
        var session = vm.Services.Session;
        if (session.State.Y != vm.Services.Content.Config.StartY - 1)
        {
            throw new InvalidOperationException("Party did not move.");
        }
        _ = _window.CaptureRenderedFrame() ?? throw new InvalidOperationException("Nothing rendered.");
    }

    /// <summary>Walks the party to a cell using the normal movement commands (turn + step), avoiding teleports.</summary>
    private static bool WalkTo(GameViewModel game, GameSession s, int tx, int ty)
    {
        var map = s.CurrentMap;
        var path = FindPath(s, s.State.X, s.State.Y, tx, ty);
        if (path is null)
        {
            return false;
        }
        foreach (var dir in path)
        {
            while (s.State.Facing != dir)
            {
                game.TurnRightCommand.Execute(null);
            }
            game.ForwardCommand.Execute(null);
            ResolveInterruptions(game, s);
            if (s.CurrentMap != map)
            {
                return false;
            }
        }
        return true;
    }

    private static void ResolveInterruptions(GameViewModel game, GameSession s)
    {
        if (game.Overlay is null && game.InCombat)
        {
            AutoBattle(s);
            game.CombatFinished();
            foreach (var c in s.State.Party.Where(c => c.IsAlive))
            {
                Rulebook.Heal(c, c.MaxHp); // keep the demo party healthy between fights
            }
        }
        if (game.Overlay is StoryViewModel && s.Combat is not null)
        {
            game.BeginCombat();
            AutoBattle(s);
            game.CombatFinished();
        }
        if (game.Overlay is not null)
        {
            game.CloseOverlay();
        }
    }

    private static void AutoBattle(GameSession s)
    {
        var combat = s.Combat!;
        combat.Advance();
        var guard = 0;
        while (combat.Outcome == CombatOutcome.Ongoing && guard++ < 400)
        {
            var c = combat.ActiveCharacter!;
            var action = combat.IsInFrontRank(c) ? new CombatAction(CombatActionKind.Attack)
                : s.Rules.HasMissileWeapon(c) ? new CombatAction(CombatActionKind.Shoot)
                : new CombatAction(CombatActionKind.Block);
            combat.Act(action);
        }
        s.EndCombat();
    }

    private static List<Direction>? FindPath(GameSession s, int sx, int sy, int tx, int ty)
    {
        var map = s.CurrentMap;
        var blocked = map.AllEvents.Where(e => e.Type is MapEventKind.Teleport or MapEventKind.Encounter or MapEventKind.Trap or MapEventKind.Spinner || e.Blocking)
            .Select(e => (e.X, e.Y)).ToHashSet();
        blocked.Remove((tx, ty));
        var prev = new Dictionary<(int, int), ((int, int) From, Direction Dir)>();
        var queue = new Queue<(int, int)>();
        queue.Enqueue((sx, sy));
        prev[(sx, sy)] = ((sx, sy), Direction.North);
        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            if ((x, y) == (tx, ty))
            {
                var dirs = new List<Direction>();
                var cur = (x, y);
                while (cur != (sx, sy))
                {
                    var p = prev[cur];
                    dirs.Add(p.Dir);
                    cur = p.From;
                }
                dirs.Reverse();
                return dirs;
            }
            foreach (var d in Enum.GetValues<Direction>())
            {
                var n = (x + d.Dx(), y + d.Dy());
                if (!s.CanPass(x, y, d) || blocked.Contains(n) || prev.ContainsKey(n))
                {
                    continue;
                }
                prev[n] = ((x, y), d);
                queue.Enqueue(n);
            }
        }
        return null;
    }

    private static void Face(GameViewModel game, GameSession s, Direction d)
    {
        while (s.State.Facing != d)
        {
            game.TurnRightCommand.Execute(null);
        }
    }

    private static void Screenshots(MainViewModel vm, string dir)
    {
        var s = vm.Services.Session;
        Capture(dir, "01-title");

        vm.ShowNewGame();
        var creation = (PartyCreationViewModel)vm.CurrentScreen;
        creation.Name = "Aldric";
        Capture(dir, "02-character-creation");

        creation.QuickPartyCommand.Execute(null);
        creation.BeginCommand.Execute(null);
        var game = vm.Game!;
        Capture(dir, "03-intro");
        game.CloseOverlay();

        // Town: look up the main street from the gate.
        Capture(dir, "04-town");

        // Walk around town (real moves) so the automap has something to show, then visit the smithy.
        foreach (var (x, y) in new[] { (1, 13), (1, 1), (6, 1), (8, 3), (14, 1), (14, 13), (9, 10), (7, 13) })
        {
            WalkTo(game, s, x, y);
        }
        WalkTo(game, s, 10, 7);
        Face(game, s, Direction.East);
        game.ForwardCommand.Execute(null); // steps into the smithy doorway
        Capture(dir, "05-shop");
        game.CloseOverlay();
        Face(game, s, Direction.West);
        game.ForwardCommand.Execute(null);
        game.AutomapCommand.Execute(null);
        Capture(dir, "06-automap-town");
        game.CloseOverlay();

        // Into the cellars beneath the well house.
        WalkTo(game, s, 12, 14);
        Face(game, s, Direction.North);
        game.ForwardCommand.Execute(null);
        ResolveInterruptions(game, s);
        var mirela = s.State.Party.First(c => c.Class == "cleric");
        game.AddMessages(s.Spells.Cast(mirela, s.Content.Spell("c_light"), s.State, null, -1, null).Messages);
        game.Refresh();
        Capture(dir, "07-dungeon-entrance");
        WalkTo(game, s, 7, 9);
        Face(game, s, Direction.North);
        game.Refresh();
        Capture(dir, "08-dungeon");

        // A fight with the cellar's kobolds.
        var result = new StepResult();
        s.StartCombat(CombatEngine.Spawn(s.Content.Monster("kobold"), 3, s.Random)
            .Concat(CombatEngine.Spawn(s.Content.Monster("cellar_rat"), 2, s.Random)), result);
        game.AddMessages(result.Messages);
        game.BeginCombat();
        var combatVm = game.Combat!;
        combatVm.FightCommand.Execute(null);
        for (var i = 0; i < 3 && combatVm.IsAction; i++)
        {
            if (combatVm.CanMelee)
            {
                combatVm.AttackCommand.Execute(null);
            }
            else if (combatVm.CanShoot)
            {
                combatVm.ShootCommand.Execute(null);
            }
            else
            {
                combatVm.BlockCommand.Execute(null);
            }
        }
        Capture(dir, "09-combat");
        if (s.Combat is not null)
        {
            AutoBattle(s);
        }
        game.CombatFinished();
        game.CloseOverlay();

        game.OpenMemberCommand.Execute(game.Party[0]);
        Capture(dir, "10-inventory");
        game.CloseOverlay();

        // Explore part of the cellars for the dungeon automap.
        foreach (var (x, y) in new[] { (5, 11), (0, 8), (1, 4), (6, 5), (9, 13), (12, 12), (14, 7) })
        {
            WalkTo(game, s, x, y);
        }
        mirela.Sp = mirela.MaxSp;
        game.AddMessages(s.Spells.Cast(mirela, s.Content.Spell("c_light"), s.State, null, -1, null).Messages);
        for (var i = 0; i < 4 && s.CurrentMap.Probe(s.State.X, s.State.Y, s.State.Facing).Wall != AVAMMB1.Core.World.WallKind.None; i++)
        {
            game.TurnRightCommand.Execute(null); // face down a corridor
        }
        game.Refresh();
        game.AutomapCommand.Execute(null);
        Capture(dir, "11-automap-dungeon");
        game.CloseOverlay();

        game.CastCommand.Execute(null);
        Capture(dir, "12-spells");
        game.CloseOverlay();

        // A guardian standing in its alcove: walk to the square before the spiders' web and look in.
        var toSpiders = FindPath(s, s.State.X, s.State.Y, 7, 2);
        if (toSpiders is { Count: > 1 })
        {
            foreach (var d in toSpiders.Take(toSpiders.Count - 2))
            {
                Face(game, s, d);
                game.ForwardCommand.Execute(null);
                ResolveInterruptions(game, s);
            }
            Face(game, s, toSpiders[^1]);
            s.State.LightSteps = Math.Max(s.State.LightSteps, 50);
            game.Refresh();
            Capture(dir, "15-guardian");
        }

        // Find the cellars' hidden room: walk to the drafty wall and search until the door shows up.
        WalkTo(game, s, 4, 3);
        ResolveInterruptions(game, s);
        Face(game, s, Direction.West);
        for (var i = 0; i < 30 && !s.CanPass(4, 3, Direction.West); i++)
        {
            game.SearchCommand.Execute(null);
            ResolveInterruptions(game, s);
            if ((s.State.X, s.State.Y) != (4, 3))
            {
                WalkTo(game, s, 4, 3);
                Face(game, s, Direction.West);
            }
        }
        mirela.Sp = mirela.MaxSp;
        s.State.LightSteps = Math.Max(s.State.LightSteps, 50);
        game.Refresh();
        Capture(dir, "14-secret-door");

        // Show the real defaults on the settings screen (animations were only disabled for capturing).
        vm.Services.Settings.SmoothMovement = true;
        vm.Services.Settings.AnimateMonsters = true;
        vm.ShowSettings(game);
        Capture(dir, "13-settings");
    }
}
