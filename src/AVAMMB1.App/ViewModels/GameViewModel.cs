using System.Collections.ObjectModel;
using Avalonia.Input;
using AVAMMB1.App.Rendering;
using AVAMMB1.App.Services;
using AVAMMB1.Core.Content;
using AVAMMB1.Core.Persistence;
using AVAMMB1.Core.Rules;
using AVAMMB1.Core.Session;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AVAMMB1.App.ViewModels;

/// <summary>Snapshot used by the map controls; a new instance forces a redraw.</summary>
/// <param name="Session">Session to draw.</param>
/// <param name="Version">Change counter.</param>
public sealed record MapSnapshot(GameSession Session, int Version)
{
    /// <summary>Where open quests lead (empty when quest markers are off).</summary>
    public IReadOnlyList<QuestGoal> Goals { get; init; } = [];
}

/// <summary>Main exploration screen: 3D view, minimap, log, party and overlays.</summary>
public sealed partial class GameViewModel : ViewModelBase
{
    private const int MaxMessages = 80;
    private readonly MainViewModel _main;
    private int _version;

    /// <summary>Creates the exploration screen for the current session.</summary>
    /// <param name="main">Root view model.</param>
    public GameViewModel(MainViewModel main)
    {
        // Re-show the log so its colours follow a theme change.
        Theming.Theme.Changed += () =>
        {
            var lines = Messages.ToList();
            Messages.Clear();
            foreach (var m in lines)
            {
                Messages.Add(m);
            }
        };
        _main = main;
        Refresh();
        _autosavedMap = Session.State.MapId;
    }

    private readonly System.Diagnostics.Stopwatch _played = System.Diagnostics.Stopwatch.StartNew();
    private string _autosavedMap;

    /// <summary>Adds the real time played since the last call to the game's play time (before saving).</summary>
    public void CountPlayTime()
    {
        var seconds = (long)_played.Elapsed.TotalSeconds;
        if (seconds > 0)
        {
            Session.State.PlaySeconds += seconds;
            _played.Restart();
        }
    }

    /// <summary>A small picture of the current view for a save slot.</summary>
    public byte[]? Thumbnail() => SaveThumbnail.Render(Scene);

    /// <summary>Saves into the rotating autosave slots (if autosave is on).</summary>
    /// <param name="why">Shown in the log, e.g. "entering Brindlemoor Cellars" (null: save quietly).</param>
    public void AutoSave(string? why)
    {
        var ironman = Session.State.Ironman;
        if (!Session.IsActive || (!ironman && !Services.Settings.Autosave))
        {
            return;
        }
        try
        {
            CountPlayTime();
            if (ironman)
            {
                Services.Saves.Save(SaveGameService.IronmanSlot, "Ironman", Session.LocationSummary, Session.State, Thumbnail());
            }
            else
            {
                Services.Saves.AutoSave(Session.LocationSummary, Session.State, Thumbnail());
            }
            if (why is not null)
            {
                AddMessages([new GameMessage($"{(ironman ? "Saved" : "Autosaved")} ({why}).", MessageKind.Info)]);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AddMessages([new GameMessage("Autosave failed: " + ex.Message, MessageKind.Bad)]);
        }
    }

    /// <summary>Services.</summary>
    public GameServices Services => _main.Services;

    private GameSession Session => _main.Services.Session;

    /// <summary>Root view model.</summary>
    public MainViewModel Main => _main;

    /// <summary>Message log.</summary>
    public ObservableCollection<MessageViewModel> Messages { get; } = new();

    /// <summary>Party members.</summary>
    public ObservableCollection<PartyMemberViewModel> Party { get; } = new();

    /// <summary>Current 3D scene.</summary>
    [ObservableProperty]
    private SceneDescription? _scene;

    /// <summary>Map snapshot for minimap and automap.</summary>
    [ObservableProperty]
    private MapSnapshot? _mapInfo;

    /// <summary>Overlay dialog (shop, combat, character sheet...), or null.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOverlay))]
    private ViewModelBase? _overlay;

    /// <summary>Whether an overlay is open.</summary>
    public bool HasOverlay => Overlay is not null;

    /// <summary>The battle in progress, shown in the 3D view and the side panel (null when exploring).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InCombat), nameof(Exploring))]
    private CombatViewModel? _combat;

    /// <summary>Whether a battle is in progress.</summary>
    public bool InCombat => Combat is not null;

    /// <summary>Whether the exploration side panel is shown (no battle).</summary>
    public bool Exploring => Combat is null;

    /// <summary>Whether monsters and other billboards animate.</summary>
    public bool AnimateSprites => Services.Settings.AnimateMonsters;

    /// <summary>Starts showing the current battle.</summary>
    public void BeginCombat()
    {
        Overlay = null;
        if (Session.Combat is { } engine)
        {
            Combat = new CombatViewModel(this, engine);
            Refresh();
        }
    }

    /// <summary>Location name.</summary>
    public string LocationText => Session.CurrentMap.Def.Name;
    /// <summary>Coordinates and facing.</summary>
    public string CompassText => $"{Session.State.Facing}  ({Session.State.X},{Session.State.Y})";
    /// <summary>Party gold and gems.</summary>
    public string GoldText => $"Purse {Session.State.Gold}  (party {Session.State.TotalGold})   {Session.State.Gems} gems";
    /// <summary>Day counter and light.</summary>
    public string TimeText => $"Day {Session.State.Day}, {Session.State.ClockText} ({Session.State.PartOfDay})"
        + (Session.CurrentMap.IsDarkness(Session.State.X, Session.State.Y) ? "   Magical darkness" : LightText)
        + (Session.IsAntiMagicHere ? "   Anti-magic" : "");
    /// <summary>The party's light: lantern oil and torch/spell steps, or "(dark)" in an unlit dark place.</summary>
    private string LightText
    {
        get
        {
            var parts = new List<string>();
            if (AVAMMB1.Core.Items.Lanterns.Active(Session.Rules, Session.State.Party) is { } a)
            {
                var cap = Session.Rules.Def(a.Lantern).FuelCapacity;
                parts.Add(cap == 0 ? "Lantern" : $"Lantern {a.Lantern.Charges}");
            }
            else if (Session.State.Party.Any(c => AVAMMB1.Core.Items.Lanterns.Refillable(Session.Rules, c)))
            {
                parts.Add("Lantern empty");
            }
            if (Session.State.LightSteps > 0)
            {
                parts.Add($"Light {Session.State.LightSteps}");
            }
            if (parts.Count == 0 && Session.CurrentMap.Def.Dark)
            {
                parts.Add("(dark)");
            }
            return parts.Count == 0 ? "" : "   " + string.Join("  ", parts);
        }
    }

    /// <summary>Whether the minimap is visible.</summary>
    public bool ShowMinimap => Services.Settings.ShowMinimap;
    /// <summary>Whether steps and turns are animated.</summary>
    public bool SmoothMovement => Services.Settings.SmoothMovement;
    /// <summary>3D view internal image height.</summary>
    public int ViewResolution => Services.Settings.ViewResolution;
    /// <summary>Smooth scaling of the 3D view.</summary>
    public bool SmoothView => Services.Settings.SmoothView;

    /// <summary>Re-reads everything from the session.</summary>
    public void Refresh()
    {
        var state = Session.State;
        var map = Session.CurrentMap;
        var sprites = new List<SceneSprite>();
        foreach (var ev in map.AllEvents)
        {
            var key = ev.Id ?? $"{map.Id}:{ev.X}:{ev.Y}:{map.Def.Events.IndexOf(ev)}";
            // Nothing is drawn on the party's own square: they are standing at it (or in it).
            if (ev.Feature is not null && (ev.X, ev.Y) != (state.X, state.Y) && !(ev.Once && state.CompletedEvents.Contains(key)) && !(ev.OpenWhenMet && Session.RequirementsMet(ev)))
            {
                sprites.Add(new SceneSprite(ev.X, ev.Y, Services.Textures.Get("Features/" + ev.Feature)));
            }
        }
        // Monsters guarding scripted encounters stand visibly in their square until defeated.
        if (Session.Combat is null)
        {
            foreach (var ev in map.AllEvents.Where(e => e.Type == MapEventKind.Encounter && e.Monsters.Count > 0))
            {
                var key = ev.Id ?? $"{map.Id}:{ev.X}:{ev.Y}:{map.Def.Events.IndexOf(ev)}";
                if (!(ev.Once && state.CompletedEvents.Contains(key)) && Services.Content.Monsters.TryGetValue(ev.Monsters[0].Monster, out var guard))
                {
                    sprites.Add(new SceneSprite(ev.X, ev.Y, Services.Textures.Get("Monsters/" + guard.Sprite), 0.8, 0.035));
                }
            }
        }
        var range = Session.ViewDistance + 1;
        for (var y = state.Y - range; y <= state.Y + range; y++)
        {
            for (var x = state.X - range; x <= state.X + range; x++)
            {
                if (map.InBounds(x, y) && map.Terrain(x, y)?.Feature is { } feature)
                {
                    sprites.Add(new SceneSprite(x, y, Services.Textures.Get("Features/" + feature), feature == "tree" ? 1.1 : 0.5));
                }
            }
        }
        Scene = new SceneDescription
        {
            Map = map,
            X = state.X,
            Y = state.Y,
            Facing = state.Facing,
            ViewDistance = Session.ViewDistance,
            Dark = Session.IsDarkHere,
            WarmLight = (map.Def.Dark || (map.Def.Kind == MapKind.Outdoor && state.IsNight)) && Session.LanternLit,
            Night = map.Def.Kind is MapKind.Outdoor or MapKind.Town ? state.Darkness : 0,
            Weather = Services.Settings.Weather && map.Def.Kind is MapKind.Outdoor or MapKind.Town ? map.Def.Weather : null,
            SecretFound = (x, y, d) => state.IsSecretFound(map.Id, x, y, d),
            Sprites = sprites,
        };
        MapInfo = new MapSnapshot(Session, ++_version) { Goals = Services.Settings.QuestMarkers ? Session.QuestGoals() : [] };
        if (Party.Count != state.Party.Count || Party.Select(p => p.Character).Where((c, i) => !ReferenceEquals(c, state.Party[i])).Any())
        {
            Party.Clear();
            for (var i = 0; i < state.Party.Count; i++)
            {
                Party.Add(new PartyMemberViewModel(Services, state.Party[i], i));
            }
        }
        foreach (var p in Party)
        {
            p.Refresh();
        }
        if (Chronicle.Check(Session) is { Count: > 0 } earned)
        {
            AddMessages(earned);
            Services.RecordAchievements(Session.State.Achievements);
        }
        OnPropertyChanged(nameof(LocationText));
        OnPropertyChanged(nameof(CompassText));
        OnPropertyChanged(nameof(GoldText));
        OnPropertyChanged(nameof(TimeText));
        OnPropertyChanged(nameof(ShowMinimap));
        OnPropertyChanged(nameof(SmoothMovement));
        OnPropertyChanged(nameof(ViewResolution));
        OnPropertyChanged(nameof(SmoothView));
        OnPropertyChanged(nameof(AnimateSprites));
        if (Combat is null)
        {
            Services.Audio.PlayMusic(map.Def.Music);
        }
        // At night the birdsong of the green country gives way to crickets.
        Services.Audio.PlayAmbience(Session.IsNightOutside && map.Def.Ambience == "birds" ? "night" : map.Def.Ambience);
    }

    /// <summary>Adds a plain message to the log.</summary>
    /// <param name="text">Text.</param>
    public void AddMessage(string text) => AddMessages([new GameMessage(text)]);

    /// <summary>Whether party cards can be dragged to change the marching order now.</summary>
    public bool CanReorder => !InCombat && !HasOverlay && Party.Count > 1;

    /// <summary>Moves a party member to another place (drag and drop on the party cards).</summary>
    /// <param name="from">Current index.</param>
    /// <param name="to">New index.</param>
    /// <param name="fromSheet">True when asked from the character sheet (which is itself an overlay).</param>
    /// <returns>The message describing the move, or null if nothing moved.</returns>
    public string? MoveMember(int from, int to, bool fromSheet = false)
    {
        if (InCombat)
        {
            AddMessage("There is no time to change places in the middle of a battle.");
            return null;
        }
        if (HasOverlay && !fromSheet || !Session.MoveMember(from, to))
        {
            return null;
        }
        var c = Session.State.Party[to];
        var text = $"{c.Name} moves to place {to + 1}{(to < 3 ? " (front rank)" : "")}.";
        AddMessage(text);
        Refresh();
        return text;
    }

    /// <summary>Adds messages to the log and plays their sounds.</summary>
    /// <param name="messages">Messages.</param>
    public void AddMessages(IEnumerable<GameMessage> messages)
    {
        var list = messages.ToList();
        Services.PlayCues(list);
        foreach (var m in list.Where(m => m.Text.Length > 0))
        {
            Messages.Add(new MessageViewModel(m));
        }
        while (Messages.Count > MaxMessages)
        {
            Messages.RemoveAt(0);
        }
    }

    /// <summary>Shows a story dialog.</summary>
    /// <param name="title">Title.</param>
    /// <param name="text">Body.</param>
    public void ShowStory(string? title, string text) => Overlay = new StoryViewModel(this, title ?? "", text, null);

    /// <summary>Closes the current overlay.</summary>
    public void CloseOverlay()
    {
        Overlay = null;
        Refresh();
    }

    private void Apply(StepResult r)
    {
        AddMessages(r.Messages.Where(m => m.Kind != MessageKind.Story || r.StoryText is null));
        if (r.Moved && !r.CombatStarted && Services.Settings.DescribeSteps)
        {
            AddMessage(AVAMMB1.Core.World.ViewDescriber.Brief(Session));
        }
        Refresh();
        if (!r.CombatStarted && !Session.State.Party.Any(c => c.CanAct))
        {
            _main.ShowGameOver();
            return;
        }
        if (r.Victory)
        {
            Overlay = new StoryViewModel(this, r.StoryTitle ?? "Victory!", r.StoryText ?? "", _main.ShowVictory);
            return;
        }
        if (r.CombatStarted && Session.Combat is not null)
        {
            if (Session.Combat.Monsters.FirstOrDefault(m => m.Def.Boss) is { } boss)
            {
                AutoSave("before facing " + boss.Def.Name);
            }
            if (r.StoryText is not null)
            {
                Overlay = new StoryViewModel(this, r.StoryTitle ?? "", r.StoryText, BeginCombat);
            }
            else
            {
                BeginCombat();
            }
            return;
        }
        if (Session.State.MapId != _autosavedMap)
        {
            _autosavedMap = Session.State.MapId;
            AutoSave("entering " + Session.CurrentMap.Def.Name);
        }
        if (r.Interaction is { } ev)
        {
            OpenBuilding(ev);
            return;
        }
        if (r.StoryText is not null)
        {
            ShowStory(r.StoryTitle, r.StoryText);
        }
    }

    private void OpenBuilding(MapEventDef ev)
    {
        if (ev.Type == MapEventKind.Riddle)
        {
            Overlay = new RiddleViewModel(this, ev);
            return;
        }
        if (ev.Type == MapEventKind.Choice)
        {
            Overlay = new DecisionViewModel(this, ev);
            return;
        }
        Services.Audio.PlaySfx("door");
        Overlay = ev.Type switch
        {
            MapEventKind.Shop when ev.Shop is not null => new ShopViewModel(this, Services.Content.Shops[ev.Shop]),
            MapEventKind.Inn => new InnViewModel(this, ev),
            MapEventKind.Temple => new TempleViewModel(this, ev),
            MapEventKind.Tavern => new TavernViewModel(this, ev),
            MapEventKind.Training => new TrainingViewModel(this, ev),
            MapEventKind.Academy => new AcademyViewModel(this, ev),
            _ => null,
        };
    }

    /// <summary>Called by the combat overlay when a battle is over.</summary>
    public void CombatFinished()
    {
        Overlay = null;
        Combat = null;
        foreach (var p in Party)
        {
            p.IsActive = false;
        }
        if (!Session.State.Party.Any(c => c.IsAlive && !c.Has(Condition.Unconscious) && !c.Has(Condition.Paralyzed)))
        {
            _main.ShowGameOver();
            return;
        }
        Refresh();
        if (Session.State.Ironman)
        {
            AutoSave(null); // ironman keeps its one save up to date after every battle
        }
    }

    [RelayCommand] private void Forward() => Apply(Session.Move(MoveKind.Forward));
    [RelayCommand] private void Back() => Apply(Session.Move(MoveKind.Back));
    [RelayCommand] private void StrafeLeft() => Apply(Session.Move(MoveKind.StrafeLeft));
    [RelayCommand] private void StrafeRight() => Apply(Session.Move(MoveKind.StrafeRight));

    [RelayCommand]
    private void TurnLeft()
    {
        Session.TurnLeft();
        Refresh();
    }

    [RelayCommand]
    private void TurnRight()
    {
        Session.TurnRight();
        Refresh();
    }

    [RelayCommand] private void Interact() => Apply(Session.Interact());

    [RelayCommand] private void Search() => Apply(Session.Search());

    [RelayCommand]
    private void Rest()
    {
        if (Session.CurrentMap.Def.Kind == MapKind.Town)
        {
            AddMessage("Townsfolk frown at campers. You rest briefly in an alley.");
        }
        Apply(Session.Rest());
    }

    [RelayCommand] private void Cast() => Overlay = new SpellCastViewModel(this, null);

    /// <summary>Opens the travel map (in a town or the open country).</summary>
    [RelayCommand]
    public void Travel()
    {
        if (!Session.CanTravel)
        {
            AddMessage("The travel map can be used only in a town or out in the open country.");
            return;
        }
        Overlay = new TravelViewModel(this);
    }

    /// <summary>Travels to a town (from the travel map).</summary>
    /// <param name="townId">Town.</param>
    public void TravelTo(string townId)
    {
        CloseOverlay();
        Apply(Session.TravelTo(townId));
    }

    /// <summary>Describes the surroundings in the log.</summary>
    [RelayCommand]
    public void Look() => AddMessages(AVAMMB1.Core.World.ViewDescriber.Describe(Session).Select(t => new GameMessage(t, MessageKind.Story)));

    [RelayCommand] private void Characters() => Overlay = new CharacterSheetViewModel(this, 0);

    /// <summary>Opens the character sheet for one member.</summary>
    /// <param name="member">Member.</param>
    [RelayCommand]
    private void OpenMember(PartyMemberViewModel member) => Overlay = new CharacterSheetViewModel(this, member.Index);

    [RelayCommand] private void Automap() => Overlay = new AutomapViewModel(this, editNote: false);

    [RelayCommand] private void Journal() => Overlay = new JournalViewModel(this);

    [RelayCommand] private void Help() => Overlay = new HelpViewModel(Services.Settings, CloseOverlay);

    [RelayCommand] private void Note() => Overlay = new AutomapViewModel(this, editNote: true);

    [RelayCommand] private void Menu() => Overlay = new GameMenuViewModel(this);

    [RelayCommand]
    private void QuickSave()
    {
        try
        {
            CountPlayTime();
            if (Session.State.Ironman)
            {
                AutoSave("ironman: the game keeps one save");
                return;
            }
            Services.Saves.Save(0, "Quick Save", Session.LocationSummary, Session.State, Thumbnail());
            AddMessages([new GameMessage("Game saved to the quick-save slot.", MessageKind.Good, "book")]);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AddMessages([new GameMessage("Could not save: " + ex.Message, MessageKind.Bad)]);
        }
    }

    [RelayCommand]
    private void QuickLoad()
    {
        if (Session.State.Ironman)
        {
            AddMessage("Ironman: there is no going back.");
            return;
        }
        if (!Services.Saves.Exists(0))
        {
            AddMessage("There is no quick save yet (F5 to quick save).");
            return;
        }
        var error = _main.LoadSlot(0);
        if (error is not null)
        {
            AddMessages([new GameMessage("Could not load: " + error, MessageKind.Bad)]);
        }
    }

    /// <inheritdoc />
    public override bool HandleKey(Key key)
    {
        if (Overlay is null && Combat is not null)
        {
            Combat.HandleKey(key);
            return true; // no exploring while fighting
        }
        if (Overlay is not null)
        {
            if (Overlay.HandleKey(key))
            {
                return true;
            }
            if (key == Key.Escape)
            {
                CloseOverlay();
                return true;
            }
            return false;
        }
        if (key >= Key.D1 && key <= Key.D6)
        {
            var i = key - Key.D1;
            if (i < Party.Count)
            {
                OpenMember(Party[i]);
            }
            return true;
        }
        return _main.ActionFor(key) is { } action && Perform(action);
    }

    /// <summary>Performs an exploration action (from the keyboard or a controller).</summary>
    /// <param name="action">Action.</param>
    /// <returns>True when the action applies.</returns>
    public bool Perform(InputAction action)
    {
        switch (action)
        {
            case InputAction.MoveForward: Forward(); return true;
            case InputAction.MoveBack: Back(); return true;
            case InputAction.TurnLeft: TurnLeft(); return true;
            case InputAction.TurnRight: TurnRight(); return true;
            case InputAction.StrafeLeft: StrafeLeft(); return true;
            case InputAction.StrafeRight: StrafeRight(); return true;
            case InputAction.Interact: Interact(); return true;
            case InputAction.Automap: Automap(); return true;
            case InputAction.Search: Search(); return true;
            case InputAction.Characters: Characters(); return true;
            case InputAction.Cast: Cast(); return true;
            case InputAction.Rest: Rest(); return true;
            case InputAction.QuickSave: QuickSave(); return true;
            case InputAction.QuickLoad: QuickLoad(); return true;
            case InputAction.Menu: Menu(); return true;
            case InputAction.Journal: Journal(); return true;
            case InputAction.Note: Note(); return true;
            case InputAction.Help: Help(); return true;
            case InputAction.Look: Look(); return true;
            case InputAction.Travel: Travel(); return true;
            default: return false;
        }
    }

    /// <summary>What a controller's buttons should do right now.</summary>
    public AVAMMB1.Core.Input.GamepadContext GamepadContext =>
        Overlay is not null ? AVAMMB1.Core.Input.GamepadContext.Menu
        : Combat is { } c ? (c.WantsMenuNavigation ? AVAMMB1.Core.Input.GamepadContext.Menu : AVAMMB1.Core.Input.GamepadContext.Combat)
        : AVAMMB1.Core.Input.GamepadContext.Exploring;
}
