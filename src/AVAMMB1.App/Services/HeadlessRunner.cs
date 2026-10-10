using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using Avalonia.Threading;
using AVAMMB1.App.Rendering;
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
                var tracks = vm.Services.Content.Maps.Values.Select(m => "Music/" + m.Def.Music)
                    .Concat(vm.Services.Content.Maps.Values.Where(m => m.Def.Ambience is not null).Select(m => "Ambience/" + m.Def.Ambience))
                    .Concat(["Music/title", "Music/battle", "Music/boss", "Ambience/night"]).Distinct().Order().ToList();
                foreach (var t in tracks)
                {
                    OpenAlAudioService.CheckDecodes(t + ".ogg");
                }
                Console.WriteLine($"Streams: {tracks.Count} music/ambience files decode");
                var pad = GamepadService.Probe();
                if (!pad.StartsWith("bundled", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Bundled SDL2 was not loaded: " + pad);
                }
                Console.WriteLine("Gamepad: " + pad);
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

    /// <summary>Drags the first party card onto the third with real (headless) mouse input, checks the
    /// order changed without opening a character sheet, then drags it back.</summary>
    private static void CheckPartyDrag(GameViewModel game, GameSession s)
    {
        var bar = _window.GetVisualDescendants().OfType<ItemsControl>().First(c => c.Name == "PartyBar");
        Point Slot(int i) => bar.TranslatePoint(new Point(bar.Bounds.Width / 6 * (i + 0.5), bar.Bounds.Height / 2), _window)!.Value;
        void Drag(int from, int to)
        {
            _window.MouseDown(Slot(from), MouseButton.Left);
            _window.MouseMove(Slot(from) + new Point(20, 0));
            _window.MouseMove(Slot(to));
            Pump();
            _window.MouseUp(Slot(to), MouseButton.Left);
            Pump();
        }
        var before = s.State.Party.ToList();
        var logLength = game.Messages.Count;
        Drag(0, 2);
        var after = s.State.Party;
        if (!ReferenceEquals(after[2], before[0]) || !ReferenceEquals(after[0], before[1]) || game.HasOverlay)
        {
            throw new InvalidOperationException("Dragging a party card did not reorder the party: " + string.Join(", ", after.Select(c => c.Name)));
        }
        Drag(2, 0);
        if (!s.State.Party.SequenceEqual(before))
        {
            throw new InvalidOperationException("Dragging the party card back did not restore the order.");
        }
        _window.MouseDown(Slot(1), MouseButton.Left); // a plain click still opens the sheet
        _window.MouseUp(Slot(1), MouseButton.Left);
        Pump();
        if (game.Overlay is not CharacterSheetViewModel)
        {
            throw new InvalidOperationException("Clicking a party card no longer opens the character sheet.");
        }
        // Keyboard / controller route: Move right on the sheet, then back with "[".
        var sheet = (CharacterSheetViewModel)game.Overlay;
        var shown = sheet.Character;
        sheet.MoveRightCommand.Execute(null);
        if (!ReferenceEquals(s.State.Party[2], shown) || !ReferenceEquals(sheet.Character, shown))
        {
            throw new InvalidOperationException("Move right on the character sheet did not move the character.");
        }
        sheet.HandleKey(Key.OemOpenBrackets);
        if (!s.State.Party.SequenceEqual(before))
        {
            throw new InvalidOperationException("[ on the character sheet did not move the character back.");
        }
        game.CloseOverlay();
        while (game.Messages.Count > logLength)
        {
            game.Messages.RemoveAt(game.Messages.Count - 1); // keep the later screenshots' logs as they were
        }
        _window.MouseMove(new Point(2, 2)); // off the cards, so no tooltip lingers
        Pump();
        Console.WriteLine("party drag ok");
    }

    private static void Capture(string dir, string name)
    {
        Pump();
        if (_window.DataContext is MainViewModel { CurrentScreen: EndingViewModel } && !name.Contains("ending", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Screenshot {name} would show the game-over screen: the party died earlier in the tour.");
        }
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

        // 3D view cost at each resolution option (average of 30 frames, outdoors in town).
        var scene = game.Scene ?? throw new InvalidOperationException("No scene.");
        var times = new List<string>();
        foreach (var h in new[] { 300, 480, 600 })
        {
            var r = new SceneRenderer(App.Textures, h);
            var cam = Camera.At(scene.X, scene.Y, scene.Facing);
            r.Render(scene, cam);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (var i = 0; i < 30; i++)
            {
                r.Render(scene, cam with { Angle = cam.Angle + i * 0.01 });
            }
            times.Add($"{r.Width}x{r.Height} {sw.Elapsed.TotalMilliseconds / 30:F1} ms");
        }
        Console.WriteLine("Renderer: " + string.Join(", ", times));
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

    /// <summary>Walks to the exit leading to another map and takes it.</summary>
    private static void TravelTo(GameViewModel game, GameSession s, string mapId)
    {
        var exit = s.CurrentMap.AllEvents.FirstOrDefault(e => e.Type == MapEventKind.Teleport && e.Map == mapId)
            ?? throw new InvalidOperationException($"No exit to {mapId} from {s.State.MapId}.");
        for (var attempt = 0; attempt < 5 && s.State.MapId != mapId; attempt++)
        {
            WalkTo(game, s, exit.X, exit.Y);
            ResolveInterruptions(game, s);
        }
        if (s.State.MapId != mapId)
        {
            throw new InvalidOperationException($"Could not travel to {mapId}.");
        }
        game.Refresh();
    }

    /// <summary>Raises the demo party to a level with the normal level-up rules, then heals it.</summary>
    private static void TrainTo(GameSession s, int level)
    {
        foreach (var c in s.State.Party)
        {
            c.Experience = Math.Max(c.Experience, Rulebook.XpForLevel(s.Content.Class(c.Class), level));
            while (s.Rules.LevelUp(c, s.Random) is not null)
            {
            }
            c.Conditions = Condition.None;
            Rulebook.Heal(c, c.MaxHp);
        }
    }

    /// <summary>Screenshots of towns and the open country are taken in daylight (late morning).</summary>
    private static void Daylight(GameViewModel game, GameSession s)
    {
        if (s.State.MinuteOfDay is < 9 * 60 or > 16 * 60)
        {
            s.State.AdvanceTo(10 * 60);
        }
        game.Refresh();
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
        creation.NextHairCommand.Execute(null);
        creation.Hair = "long_red";
        creation.NextBeardCommand.Execute(null);
        Capture(dir, "02-character-creation");
        creation.Hair = creation.Beard = null;

        // Controller-only typing: the on-screen keyboard over the name box.
        creation.Name = "";
        var nameBox = _window.GetVisualDescendants().OfType<TextBox>().First(t => t.Watermark == "Enter a name");
        _window.OpenKeyboard(nameBox);
        Pump();
        var keyboard = _window.GetVisualDescendants().OfType<AVAMMB1.App.Controls.OnScreenKeyboard>().FirstOrDefault()
            ?? (Avalonia.Controls.Primitives.OverlayLayer.GetOverlayLayer(_window)?.GetVisualDescendants().OfType<AVAMMB1.App.Controls.OnScreenKeyboard>().FirstOrDefault())
            ?? throw new InvalidOperationException("The on-screen keyboard did not open.");
        Button KeyFor(string label) => keyboard.GetVisualDescendants().OfType<Button>().First(b => string.Equals(b.Content as string, label, StringComparison.OrdinalIgnoreCase));
        foreach (var ch in "Wren")
        {
            KeyFor(ch.ToString()).RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        }
        Pump();
        Capture(dir, "41-keyboard");
        KeyFor("Done").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Pump();
        if (creation.Name != "Wren" || _window.KeyboardOpen)
        {
            throw new InvalidOperationException($"The on-screen keyboard typed '{creation.Name}'.");
        }
        creation.RandomNameCommand.Execute(null);
        if (string.IsNullOrWhiteSpace(creation.Name))
        {
            throw new InvalidOperationException("No random name.");
        }
        creation.Name = "Aldric";
        Console.WriteLine("keyboard ok");

        creation.QuickPartyCommand.Execute(null);
        creation.BeginCommand.Execute(null);
        var game = vm.Game!;
        Capture(dir, "03-intro");
        game.CloseOverlay();

        // Town: look up the main street from the gate.
        Daylight(game, s);
        Capture(dir, "04-town");
        CheckPartyDrag(game, s);

        // The inn: rename a character and change their looks.
        var innEvent = s.CurrentMap.AllEvents.First(e => e.Type == MapEventKind.Inn);
        var inn = new InnViewModel(game, innEvent);
        game.Overlay = inn;
        inn.EditCommand.Execute(game.Party[1]);
        inn.EditName = "Dorgun Stonefist";
        inn.EditHair = "short_red";
        inn.EditBeard = "long_red";
        Pump();
        Capture(dir, "33-inn-looks");
        inn.CancelEditCommand.Execute(null);
        game.CloseOverlay();

        // Walk around town (real moves) so the automap has something to show, then visit the smithy.
        foreach (var (x, y) in new[] { (1, 13), (1, 1), (6, 1), (8, 3), (14, 1), (14, 13), (9, 10), (7, 13) })
        {
            WalkTo(game, s, x, y);
        }
        WalkTo(game, s, 10, 7);
        Face(game, s, Direction.East);
        // Some loot to sell: a club and padded armour nobody needs, and a spare long sword.
        var seller = s.State.Party[0];
        seller.Backpack.AddRange([new AVAMMB1.Core.Items.ItemInstance("club"), new AVAMMB1.Core.Items.ItemInstance("padded_armor"), new AVAMMB1.Core.Items.ItemInstance("dagger")]);
        game.ForwardCommand.Execute(null); // steps into the smithy doorway
        Capture(dir, "05-shop");
        if (game.Overlay is ShopViewModel shop)
        {
            shop.SellJunkCommand.Execute(null);
            shop.ToggleSmithingCommand.Execute(null);
            Capture(dir, "39-smithing");
            shop.ToggleSmithingCommand.Execute(null);
        }
        seller.Backpack.RemoveAll(i => i.ItemId is "club" or "padded_armor" or "dagger");
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
        for (var i = 0; i < 6 && combatVm.IsAction && !combatVm.CanCast; i++)
        {
            combatVm.BlockCommand.Execute(null);
        }
        if (combatVm.IsAction && combatVm.CanCast)
        {
            combatVm.ShowSpellsCommand.Execute(null);
            Capture(dir, "30-combat-spells");
            combatVm.HandleKey(Avalonia.Input.Key.Escape);
        }
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
        s.State.SetNote("cellars", 1, 13, "Warning: kobold king in the NE hall");
        s.State.SetNote("cellars", 7, 7, "Spring - heals and restores SP");
        game.NoteCommand.Execute(null);
        if (game.Overlay is AutomapViewModel notes)
        {
            notes.NoteText = "Dead end? Search the walls";
        }
        Capture(dir, "11-automap-dungeon");
        game.CloseOverlay();

        // The journal, as it would read after meeting Archivist Pell.
        s.State.Flags.Add("pell_met");
        s.State.CompletedEvents.Add("cellar_hint");
        game.JournalCommand.Execute(null);
        Capture(dir, "22-journal");
        game.CloseOverlay();
        game.HelpCommand.Execute(null);
        Capture(dir, "24-help");
        game.CloseOverlay();
        s.State.Flags.Remove("pell_met");

        game.CastCommand.Execute(null);
        Capture(dir, "12-spells");
        game.CloseOverlay();

        // A brass lantern: brighter (8 squares) and warmer than a torch or spell.
        var brannoc = s.State.Party[0];
        brannoc.Backpack.Add(new AVAMMB1.Core.Items.ItemInstance("lantern", s.Content.Item("lantern").Charges));
        // Through the character sheet, as a player would: select it in the backpack and press Use.
        var logLength = game.Messages.Count;
        game.OpenMemberCommand.Execute(game.Party[0]);
        var sheet = (CharacterSheetViewModel)game.Overlay!;
        sheet.SelectCommand.Execute(sheet.Backpack.Last());
        sheet.UseCommand.Execute(null);
        if (!brannoc.Equipment.TryGetValue(EquipSlot.Light, out var lit) || lit.ItemId != "lantern")
        {
            throw new InvalidOperationException("Pressing Use on a lantern did not equip it: " + sheet.Feedback);
        }
        game.CloseOverlay();
        while (game.Messages.Count > logLength)
        {
            game.Messages.RemoveAt(game.Messages.Count - 1);
        }
        s.State.LightSteps = 0;
        game.Refresh();
        Capture(dir, "28-lantern");

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

        // Down to the Old Cistern below the cellars (level 2-4 country: train the demo party up first)
        // and a look across the flooded reservoir.
        TrainTo(s, 4);
        TravelTo(game, s, "cistern");
        WalkTo(game, s, 10, 8);
        Face(game, s, Direction.West);
        s.State.LightSteps = Math.Max(s.State.LightSteps, 50);
        game.Refresh();
        Capture(dir, "21-old-cistern");
        TravelTo(game, s, "cellars");

        // The Ashen Hills expansion (level 5-7 country): travel there the long way, through each map's exits.
        TrainTo(s, 7);
        foreach (var stop in new[] { "brindlemoor", "wilds", "hills" })
        {
            TravelTo(game, s, stop);
        }
        WalkTo(game, s, 9, 12);
        Face(game, s, Direction.West);
        game.Refresh();
        Daylight(game, s);
        Capture(dir, "16-ashen-hills");
        // The same road at night: darker sky, shorter sight, the clock in the corner.
        var dayTime = s.State.Minutes;
        s.State.AdvanceTo(22 * 60);
        game.Refresh();
        Capture(dir, "29-night");
        s.State.Minutes = dayTime;
        game.Refresh();
        TravelTo(game, s, "thornwick");
        Daylight(game, s);
        Capture(dir, "17-thornwick");
        TravelTo(game, s, "hills");
        TravelTo(game, s, "duskmere");
        Face(game, s, Direction.West);
        game.Refresh();
        Daylight(game, s);
        Capture(dir, "18-duskmere");

        // The Sunscar Coast is level 9-12 country: train the demo party up first.
        TrainTo(s, 12);
        s.State.Gold = Math.Max(s.State.Gold, 500);
        // Back over the pass, through Saltreach and across on the ferry.
        foreach (var stop in new[] { "hills", "wilds", "saltreach", "ashkar" })
        {
            TravelTo(game, s, stop);
        }
        WalkTo(game, s, 12, 10);
        Face(game, s, Direction.West);
        game.Refresh();
        Daylight(game, s);
        Capture(dir, "19-port-ashkar");
        TravelTo(game, s, "sunscar");
        WalkTo(game, s, 9, 14);
        Face(game, s, Direction.East);
        game.Refresh();
        Daylight(game, s);
        Capture(dir, "20-sunscar-oasis");

        // North by ship to Wintermere (level 11-14 country) and into the Rime Halls.
        TrainTo(s, 14);
        s.State.Gold = Math.Max(s.State.Gold, 1000);
        foreach (var stop in new[] { "ashkar", "saltreach", "wintermere" })
        {
            TravelTo(game, s, stop);
        }
        WalkTo(game, s, 7, 9);
        Face(game, s, Direction.North);
        game.Refresh();
        Daylight(game, s);
        Capture(dir, "25-wintermere");
        TravelTo(game, s, "frostmark");
        TravelTo(game, s, "rime1");
        WalkTo(game, s, 7, 12);
        for (var i = 0; i < 4 && s.CurrentMap.Probe(s.State.X, s.State.Y, s.State.Facing).Wall != AVAMMB1.Core.World.WallKind.None; i++)
        {
            game.TurnRightCommand.Execute(null); // face down a corridor
        }
        s.State.LightSteps = Math.Max(s.State.LightSteps, 50);
        game.Refresh();
        Capture(dir, "26-rime-halls");

        // The Silent Choir: the Choirmaster beneath the Hollow Bell (then back to the Rime Halls).
        var beforeChoir = (s.State.MapId, s.State.X, s.State.Y, s.State.Facing);
        (s.State.MapId, s.State.X, s.State.Y, s.State.Facing) = ("belfry2", 12, 5, Direction.North);
        s.State.LightSteps = Math.Max(s.State.LightSteps, 50);
        var choir = new StepResult();
        s.StartCombat(CombatEngine.Spawn(s.Content.Monster("choirmaster"), 1, s.Random)
            .Concat(CombatEngine.Spawn(s.Content.Monster("choir_cantor"), 2, s.Random)), choir);
        game.Refresh();
        game.BeginCombat();
        Pump();
        Capture(dir, "32-choirmaster");
        foreach (var m in s.Combat!.Monsters)
        {
            m.Hp = 1; // the tour only needs the picture: the next blows end the fight in the party's favour
        }
        // Slow battle text: lines are revealed one at a time; acting (or Continue) shows the rest.
        vm.Services.Settings.BattleTextSpeed = 3;
        game.Combat!.FightCommand.Execute(null);
        var held = false;
        for (var i = 0; i < 12 && !held && s.Combat is not null && game.Combat.IsAction; i++)
        {
            game.Combat.BlockCommand.Execute(null); // the monsters answer with several lines
            held = game.Combat.IsRevealing;
        }
        if (!held)
        {
            throw new InvalidOperationException("Slow battle text never held lines back.");
        }
        game.Combat.FlushLog();
        if (game.Combat.IsRevealing)
        {
            throw new InvalidOperationException("Flushing the battle text left lines waiting.");
        }
        vm.Services.Settings.BattleTextSpeed = 0;
        Console.WriteLine("battle text ok");
        if (s.Combat is not null)
        {
            AutoBattle(s);
        }
        game.CombatFinished();
        game.CloseOverlay();
        (s.State.MapId, s.State.X, s.State.Y, s.State.Facing) = beforeChoir;
        game.Refresh();

        // Accessibility: the high-contrast theme with larger text, and the sharper 3D view.
        var settings = vm.Services.Settings;
        settings.ColorTheme = "HighContrast";
        settings.TextScale = 115;
        settings.ViewResolution = 600;
        MainViewModel.ApplyTheme(settings);
        game.Refresh();
        Pump();
        Capture(dir, "23-high-contrast");
        settings.SmoothView = false;
        settings.ColorTheme = "Standard";
        settings.TextScale = 100;
        settings.ViewResolution = 300;
        MainViewModel.ApplyTheme(settings);
        game.Refresh();

        // Detailed textures at 800 x 600, looking down a corridor of the Hollow Crypt.
        var crypt = s.Content.Map("crypt").AllEvents.First(e => e.Type == MapEventKind.Teleport);
        (s.State.MapId, s.State.X, s.State.Y, s.State.Facing) = ("crypt", crypt.X, crypt.Y, Direction.North);
        s.State.LightSteps = 50;
        vm.Services.Textures.Detailed = true;
        settings.ViewResolution = 600;
        game.Refresh();
        Pump();
        Capture(dir, "27-detailed-textures");
        vm.Services.Textures.Detailed = false;
        settings.ViewResolution = 300;
        game.Refresh();

        // A riddle door in the Hollow Belfry, and Sister Veyl's choice in Duskmere.
        var beforePuzzles = (s.State.MapId, s.State.X, s.State.Y, s.State.Facing);
        (s.State.MapId, s.State.X, s.State.Y, s.State.Facing) = ("belfry1", 13, 3, Direction.South);
        s.State.LightSteps = Math.Max(s.State.LightSteps, 50);
        game.Refresh();
        var riddleStep = s.Interact();
        game.Overlay = riddleStep.Interaction is { } riddleEv ? new RiddleViewModel(game, riddleEv) : throw new InvalidOperationException("No riddle at the Silent Door.");
        ((RiddleViewModel)game.Overlay).Answer = "an echo";
        ((RiddleViewModel)game.Overlay).SubmitCommand.Execute(null);
        Capture(dir, "37-riddle");
        game.CloseOverlay();
        s.State.Flags.Add("choir_saltreach");
        (s.State.MapId, s.State.X, s.State.Y, s.State.Facing) = ("duskmere", 4, 4, Direction.North);
        Daylight(game, s);
        game.Refresh();
        (s.State.X, s.State.Y) = (4, 3);
        game.Overlay = s.Interact().Interaction is { } veyl ? new DecisionViewModel(game, veyl) : throw new InvalidOperationException("No choice at Veyl's hut.");
        Capture(dir, "38-choice");
        game.CloseOverlay();
        s.State.Flags.Remove("choir_saltreach");
        (s.State.MapId, s.State.X, s.State.Y, s.State.Facing) = beforePuzzles;
        game.Refresh();

        // The bestiary, after the battles of the screenshot tour.
        game.JournalCommand.Execute(null);
        ((JournalViewModel)game.Overlay!).Tab = 1;
        Pump();
        Capture(dir, "34-bestiary");
        ((JournalViewModel)game.Overlay!).Tab = 2;
        Pump();
        Capture(dir, "35-items");
        ((JournalViewModel)game.Overlay!).Tab = 3;
        Pump();
        Capture(dir, "36-chronicle");
        game.CloseOverlay();

        // The load screen: autosaves (some written on the way here, one now) and a quick save, with pictures.
        game.AutoSave("screenshot");
        game.QuickSaveCommand.Execute(null);
        game.Overlay = new SaveLoadViewModel(vm, saving: false, onClose: game.CloseOverlay);
        Pump();
        Capture(dir, "31-load-game");
        game.CloseOverlay();

        // Show the real defaults on the settings screen (animations were only disabled for capturing).
        vm.Services.Settings.SmoothMovement = true;
        vm.Services.Settings.AnimateMonsters = true;
        vm.ShowSettings(game);
        Capture(dir, "13-settings");
        vm.ReturnToGame();

        // Hall of Fame: one victory, then an ironman run that falls (its save is deleted, its deeds kept).
        s.State.Won = true;
        vm.Services.RecordRun("Victory");
        s.State.Won = false;
        s.State.Ironman = true;
        game.AutoSave(null);
        if (!vm.Services.Saves.Exists(SaveGameService.IronmanSlot))
        {
            throw new InvalidOperationException("The ironman run was not saved.");
        }
        game.QuickLoadCommand.Execute(null);
        if (!game.Messages.Any(m => m.Text.Contains("no going back", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("Quick load was allowed in an ironman run.");
        }
        vm.ShowGameOver();
        if (vm.Services.Saves.Exists(SaveGameService.IronmanSlot) || vm.Services.HallOfFameStore.Load().Entries.Count != 2)
        {
            throw new InvalidOperationException("The fallen ironman run was not recorded properly.");
        }
        vm.ShowHallOfFame();
        Capture(dir, "40-hall-of-fame");
        Console.WriteLine("ironman ok");
    }
}
