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
public sealed record MapSnapshot(GameSession Session, int Version);

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
        _main = main;
        Refresh();
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

    /// <summary>Location name.</summary>
    public string LocationText => Session.CurrentMap.Def.Name;
    /// <summary>Coordinates and facing.</summary>
    public string CompassText => $"{Session.State.Facing}  ({Session.State.X},{Session.State.Y})";
    /// <summary>Party gold and gems.</summary>
    public string GoldText => $"{Session.State.Gold} gold   {Session.State.Gems} gems";
    /// <summary>Day counter and light.</summary>
    public string TimeText => $"Day {Session.State.Day}" + (Session.State.LightSteps > 0 ? $"   Light {Session.State.LightSteps}" : Session.CurrentMap.Def.Dark ? "   (dark)" : "");
    /// <summary>Whether the minimap is visible.</summary>
    public bool ShowMinimap => Services.Settings.ShowMinimap;
    /// <summary>Whether steps and turns are animated.</summary>
    public bool SmoothMovement => Services.Settings.SmoothMovement;

    /// <summary>Re-reads everything from the session.</summary>
    public void Refresh()
    {
        var state = Session.State;
        var map = Session.CurrentMap;
        var sprites = new List<SceneSprite>();
        foreach (var ev in map.AllEvents)
        {
            var key = ev.Id ?? $"{map.Id}:{ev.X}:{ev.Y}:{map.Def.Events.IndexOf(ev)}";
            if (ev.Feature is not null && !(ev.Once && state.CompletedEvents.Contains(key)))
            {
                sprites.Add(new SceneSprite(ev.X, ev.Y, Services.Textures.Get("Features/" + ev.Feature)));
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
            SecretFound = (x, y, d) => state.IsSecretFound(map.Id, x, y, d),
            Sprites = sprites,
        };
        MapInfo = new MapSnapshot(Session, ++_version);
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
        OnPropertyChanged(nameof(LocationText));
        OnPropertyChanged(nameof(CompassText));
        OnPropertyChanged(nameof(GoldText));
        OnPropertyChanged(nameof(TimeText));
        OnPropertyChanged(nameof(ShowMinimap));
        OnPropertyChanged(nameof(SmoothMovement));
        if (Overlay is not CombatViewModel)
        {
            Services.Audio.PlayMusic(map.Def.Music);
        }
    }

    /// <summary>Adds a plain message to the log.</summary>
    /// <param name="text">Text.</param>
    public void AddMessage(string text) => AddMessages([new GameMessage(text)]);

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
            if (r.StoryText is not null)
            {
                Overlay = new StoryViewModel(this, r.StoryTitle ?? "", r.StoryText, () => Overlay = new CombatViewModel(this, Session.Combat!));
            }
            else
            {
                Overlay = new CombatViewModel(this, Session.Combat);
            }
            return;
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
        Services.Audio.PlaySfx("door");
        Overlay = ev.Type switch
        {
            MapEventKind.Shop when ev.Shop is not null => new ShopViewModel(this, Services.Content.Shops[ev.Shop]),
            MapEventKind.Inn => new InnViewModel(this, ev),
            MapEventKind.Temple => new TempleViewModel(this, ev),
            MapEventKind.Tavern => new TavernViewModel(this, ev),
            MapEventKind.Training => new TrainingViewModel(this, ev),
            _ => null,
        };
    }

    /// <summary>Called by the combat overlay when a battle is over.</summary>
    public void CombatFinished()
    {
        Overlay = null;
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

    [RelayCommand] private void Characters() => Overlay = new CharacterSheetViewModel(this, 0);

    /// <summary>Opens the character sheet for one member.</summary>
    /// <param name="member">Member.</param>
    [RelayCommand]
    private void OpenMember(PartyMemberViewModel member) => Overlay = new CharacterSheetViewModel(this, member.Index);

    [RelayCommand] private void Automap() => Overlay = new AutomapViewModel(this);

    [RelayCommand] private void Menu() => Overlay = new GameMenuViewModel(this);

    [RelayCommand]
    private void QuickSave()
    {
        try
        {
            Services.Saves.Save(0, "Quick Save", Session.LocationSummary, Session.State);
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
        switch (_main.ActionFor(key))
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
            default: return false;
        }
    }
}
