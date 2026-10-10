using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Input;
using AVAMMB1.Core.Content;
using AVAMMB1.Core.Info;
using AVAMMB1.Core.Persistence;
using AVAMMB1.Core.Rules;
using AVAMMB1.Core.World;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AVAMMB1.App.ViewModels;

/// <summary>A tool in the map editor.</summary>
public enum EditorTool
{
    /// <summary>Click a square's side: wall.</summary>
    Wall,
    /// <summary>Door.</summary>
    Door,
    /// <summary>Locked door.</summary>
    LockedDoor,
    /// <summary>Secret door.</summary>
    SecretDoor,
    /// <summary>Removes a wall or door.</summary>
    Open,
    /// <summary>Solid rock square.</summary>
    Rock,
    /// <summary>Plain floor square.</summary>
    Floor,
    /// <summary>Magical darkness square.</summary>
    Darkness,
    /// <summary>Anti-magic square.</summary>
    AntiMagic,
    /// <summary>Places a new event.</summary>
    Event,
    /// <summary>Where a test play starts.</summary>
    Start,
}

/// <summary>A tool button.</summary>
/// <param name="Tool">The tool.</param>
/// <param name="Label">Label.</param>
/// <param name="Tip">Tooltip.</param>
public sealed record ToolRow(EditorTool Tool, string Label, string Tip);

/// <summary>An event on the selected square.</summary>
/// <param name="Index">Index in the map's event list.</param>
/// <param name="Label">What it is.</param>
public sealed record EventRow(int Index, string Label);

/// <summary>
/// The map editor: draw walls, doors and squares, place events, set the map's look and encounters, check
/// it, save it into a mod pack and test-play it with a party.
/// </summary>
public sealed partial class MapEditorViewModel : ViewModelBase
{
    private readonly MainViewModel _main;
    private bool _dirty;

    /// <summary>Opens the editor on a new map.</summary>
    /// <param name="main">Root view model.</param>
    public MapEditorViewModel(MainViewModel main)
    {
        _main = main;
        OpenableMaps = main.Services.Content.Maps.Keys.Where(k => k != Depths.MapId).Order(StringComparer.Ordinal).ToArray();
        _draft = MapDraft.New("my_dungeon", "My Dungeon", MapKind.Dungeon, 12, 10);
        _draft.Events.Add(new MapEventDef { X = 0, Y = 9, Type = MapEventKind.Teleport, Name = "Back to Brindlemoor", Map = "brindlemoor", ToX = 7, ToY = 14, Facing = Direction.North });
        LoadSettings();
        Start = new PixelPoint(0, 9);
        Select(0, 9);
        Check();
    }

    /// <summary>The map being edited.</summary>
    [ObservableProperty]
    private MapDraft _draft;

    /// <summary>Changes on every edit (redraws the map).</summary>
    [ObservableProperty]
    private int _version;

    /// <summary>The tools.</summary>
    public IReadOnlyList<ToolRow> Tools { get; } =
    [
        new(EditorTool.Wall, Loc.T("Wall"), Loc.T("Click near a square's side to put a wall there")),
        new(EditorTool.Door, Loc.T("Door"), Loc.T("A door on a square's side")),
        new(EditorTool.LockedDoor, Loc.T("Locked"), Loc.T("A locked door: robbers can pick it, or the map's key or flag opens it")),
        new(EditorTool.SecretDoor, Loc.T("Secret"), Loc.T("A secret door: a wall until the party searches next to it")),
        new(EditorTool.Open, Loc.T("Open"), Loc.T("Removes a wall or door")),
        new(EditorTool.Rock, Loc.T("Rock"), Loc.T("A solid square")),
        new(EditorTool.Floor, Loc.T("Floor"), Loc.T("A plain floor square")),
        new(EditorTool.Darkness, Loc.T("Darkness"), Loc.T("Magical darkness: no light helps")),
        new(EditorTool.AntiMagic, Loc.T("Anti-magic"), Loc.T("No spells, scrolls or wands")),
        new(EditorTool.Event, Loc.T("Event"), Loc.T("Places a new event of the type chosen below")),
        new(EditorTool.Start, Loc.T("Start"), Loc.T("Where the party starts a test play")),
    ];

    /// <summary>The tool in use.</summary>
    [ObservableProperty]
    private EditorTool _tool = EditorTool.Wall;

    /// <summary>The selected square.</summary>
    [ObservableProperty]
    private PixelPoint _selected = new(-1, -1);

    /// <summary>Where a test play starts.</summary>
    [ObservableProperty]
    private PixelPoint _start = new(-1, -1);

    /// <summary>Picks a tool.</summary>
    /// <param name="tool">The tool.</param>
    [RelayCommand]
    private void UseTool(EditorTool tool) => Tool = tool;

    /// <summary>The selected tool's name, for the status line.</summary>
    public string ToolText => Tools.First(t => t.Tool == Tool).Label;

    partial void OnToolChanged(EditorTool value) => OnPropertyChanged(nameof(ToolText));

    // ------------------------------------------------------------------ editing the grid

    /// <summary>A square (and maybe one of its sides) was clicked or painted over.</summary>
    /// <param name="x">X.</param>
    /// <param name="y">Y.</param>
    /// <param name="side">The side near the pointer, if any.</param>
    /// <param name="drag">Whether this is part of a drag.</param>
    public void Click(int x, int y, CellSide? side, bool drag)
    {
        var edge = Tool switch
        {
            EditorTool.Wall => 'W',
            EditorTool.Door => 'D',
            EditorTool.LockedDoor => 'L',
            EditorTool.SecretDoor => 'S',
            EditorTool.Open => ' ',
            _ => (char?)null,
        };
        if (edge is { } kind)
        {
            if (side is { } s)
            {
                Draft.SetEdge(x, y, s, kind);
                Changed();
            }
            else if (!drag && kind is 'W' or ' ')
            {
                Select(x, y); // a square's middle: select it (walls go on its sides)
            }
            return;
        }
        switch (Tool)
        {
            case EditorTool.Rock:
                Draft.SetCell(x, y, '#');
                break;
            case EditorTool.Floor:
                Draft.SetCell(x, y, '.');
                break;
            case EditorTool.Darkness:
                Draft.Meta.Terrain.TryAdd("d", new TerrainDef { Darkness = true, Name = "magical darkness", MapColor = "#101018" });
                Draft.SetCell(x, y, 'd');
                break;
            case EditorTool.AntiMagic:
                Draft.Meta.Terrain.TryAdd("a", new TerrainDef { AntiMagic = true, Name = "anti-magic", MapColor = "#402858" });
                Draft.SetCell(x, y, 'a');
                break;
            case EditorTool.Event when !drag:
                Draft.Events.Add(NewEvent((MapEventKind)NewEventTypeIndex, x, y));
                Select(x, y);
                SelectedEvent = CellEvents.LastOrDefault();
                break;
            case EditorTool.Start:
                Start = new PixelPoint(x, y);
                break;
            default:
                return;
        }
        Select(x, y);
        Changed();
    }

    private void Changed()
    {
        _dirty = true;
        Version++;
        Check();
    }

    private void Select(int x, int y)
    {
        Selected = new PixelPoint(x, y);
        RefreshCellEvents();
    }

    /// <summary>Selected square, as text.</summary>
    public string SelectedText => Draft.InBounds(Selected.X, Selected.Y)
        ? Loc.F("Square ({0},{1}): {2}", Selected.X, Selected.Y, Draft.Cell(Selected.X, Selected.Y) switch { '.' => Loc.T("floor"), '#' => Loc.T("rock"), 'd' => Loc.T("darkness"), 'a' => Loc.T("anti-magic"), var c => c.ToString() })
        : Loc.T("Click a square to select it.");

    partial void OnSelectedChanged(PixelPoint value) => OnPropertyChanged(nameof(SelectedText));

    // ------------------------------------------------------------------ events

    /// <summary>Event types for new events.</summary>
    public string[] EventTypes { get; } = Enum.GetNames<MapEventKind>();

    /// <summary>The type the Event tool places.</summary>
    [ObservableProperty]
    private int _newEventTypeIndex = (int)MapEventKind.Treasure;

    /// <summary>The events on the selected square.</summary>
    public ObservableCollection<EventRow> CellEvents { get; } = new();

    /// <summary>The event being edited.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEvent))]
    private EventRow? _selectedEvent;

    /// <summary>Whether an event is selected.</summary>
    public bool HasEvent => SelectedEvent is not null;

    /// <summary>The selected event as JSON, to edit (see docs/CONTENT_FORMAT.md for every field).</summary>
    [ObservableProperty]
    private string _eventJson = "";

    partial void OnSelectedEventChanged(EventRow? value) =>
        EventJson = value is null ? "" : MapDraft.EventToJson(Draft.Events[value.Index]);

    private void RefreshCellEvents()
    {
        CellEvents.Clear();
        for (var i = 0; i < Draft.Events.Count; i++)
        {
            var e = Draft.Events[i];
            if (e.X == Selected.X && e.Y == Selected.Y)
            {
                CellEvents.Add(new EventRow(i, $"{e.Type}{(e.Name ?? e.Text) switch { { Length: > 0 } t => ": " + (t.Length > 40 ? t[..40] + "..." : t), _ => "" }}"));
            }
        }
        SelectedEvent = CellEvents.FirstOrDefault();
    }

    [RelayCommand]
    private void ApplyEvent()
    {
        if (SelectedEvent is not { } row)
        {
            return;
        }
        try
        {
            var e = MapDraft.EventFromJson(EventJson);
            if (!Draft.InBounds(e.X, e.Y))
            {
                Status = Loc.F("The event's square ({0},{1}) is off the map.", e.X, e.Y);
                return;
            }
            Draft.Events[row.Index] = e;
            Status = Loc.T("Event updated.");
            Select(e.X, e.Y);
            Changed();
        }
        catch (InvalidDataException ex)
        {
            Status = Loc.T("That is not a valid event: ") + ex.Message;
        }
    }

    [RelayCommand]
    private void DeleteEvent()
    {
        if (SelectedEvent is { } row)
        {
            Draft.Events.RemoveAt(row.Index);
            RefreshCellEvents();
            Changed();
        }
    }

    /// <summary>A new event of a type with sensible starting values.</summary>
    private MapEventDef NewEvent(MapEventKind type, int x, int y)
    {
        var content = _main.Services.Content;
        var e = new MapEventDef { X = x, Y = y, Type = type };
        switch (type)
        {
            case MapEventKind.Message:
                e.Text = "Words are scratched into the wall here.";
                e.Once = true;
                break;
            case MapEventKind.Treasure:
                e.Gold = AVAMMB1.Core.Dice.DiceExpression.Parse("3d20");
                e.Once = true;
                e.Feature = "chest";
                break;
            case MapEventKind.Encounter:
                var monster = Draft.Meta.Encounters.FirstOrDefault()?.Monster ?? "kobold";
                e.Monsters = [new FixedMonsterDef { Monster = monster, Count = AVAMMB1.Core.Dice.DiceExpression.Parse("1d3") }];
                e.Once = true;
                break;
            case MapEventKind.Teleport:
                e.Name = "Back to Brindlemoor";
                e.Map = "brindlemoor";
                (e.ToX, e.ToY, e.Facing) = (7, 14, Direction.North);
                break;
            case MapEventKind.Shop:
                e.Shop = content.Shops.Keys.Order(StringComparer.Ordinal).FirstOrDefault();
                e.Name = "A shop";
                break;
            case MapEventKind.Inn or MapEventKind.Temple or MapEventKind.Tavern or MapEventKind.Training or MapEventKind.Academy:
                e.Name = "The " + type;
                break;
            case MapEventKind.Trap:
                e.Trap = TrapEffect.Damage;
                e.Damage = AVAMMB1.Core.Dice.DiceExpression.Parse("1d6");
                e.Text = "Darts fly from the walls!";
                e.Once = true;
                break;
            case MapEventKind.Fountain:
                e.Heal = true;
                e.Feature = "fountain";
                break;
            case MapEventKind.Quest or MapEventKind.Victory:
                e.Text = "Describe what happens here.";
                e.Once = true;
                break;
            case MapEventKind.Riddle:
                e.Text = "What has keys but opens no locks?";
                e.Answers = ["piano", "keyboard"];
                e.SuccessText = "The door grinds open.";
                e.FailText = "Nothing happens.";
                e.Once = true;
                break;
            case MapEventKind.Choice:
                e.Text = "Two paths lie ahead.";
                e.Options = [new ChoiceOptionDef { Label = "Left", Text = "You go left." }, new ChoiceOptionDef { Label = "Right", Text = "You go right." }];
                e.Once = true;
                break;
        }
        return e;
    }

    // ------------------------------------------------------------------ map settings

    /// <summary>Map id (also the file name).</summary>
    [ObservableProperty]
    private string _mapId = "";

    /// <summary>Name shown in the game.</summary>
    [ObservableProperty]
    private string _mapName = "";

    /// <summary>Kind (town, outdoor, dungeon).</summary>
    [ObservableProperty]
    private int _kindIndex;

    /// <summary>Map kinds.</summary>
    public string[] Kinds { get; } = Enum.GetNames<MapKind>();

    /// <summary>Width.</summary>
    [ObservableProperty]
    private decimal? _mapWidth;

    /// <summary>Height.</summary>
    [ObservableProperty]
    private decimal? _mapHeight;

    /// <summary>Wall texture.</summary>
    [ObservableProperty]
    private string? _wallTexture;

    /// <summary>Floor texture.</summary>
    [ObservableProperty]
    private string? _floorTexture;

    /// <summary>Ceiling texture ("(sky)" for none).</summary>
    [ObservableProperty]
    private string? _ceilingTexture;

    /// <summary>Music.</summary>
    [ObservableProperty]
    private string? _music;

    /// <summary>Needs light.</summary>
    [ObservableProperty]
    private bool _dark;

    /// <summary>Random encounter chance per step (percent).</summary>
    [ObservableProperty]
    private decimal? _encounterChance;

    /// <summary>Random encounters, one per line: "monster count weight" (e.g. "kobold 1d4 3").</summary>
    [ObservableProperty]
    private string _encountersText = "";

    /// <summary>Wall textures to choose from.</summary>
    public string[] WallTextures { get; } = Textures("wall_");

    /// <summary>Floor textures to choose from.</summary>
    public string[] FloorTextures { get; } = Textures("floor_");

    /// <summary>Ceiling textures to choose from ("(sky)" = open sky).</summary>
    public string[] CeilingTextures { get; } = ["(sky)", .. Textures("wall_")];

    /// <summary>Music to choose from.</summary>
    public string[] MusicKeys { get; } = ["town", "dungeon", "caves", "desert", "marsh", "frost", "title"];

    private static string[] Textures(string prefix)
    {
        try
        {
            return Avalonia.Platform.AssetLoader.GetAssets(new Uri("avares://AVAMMB1/Assets/Graphics/Textures/"), null)
                .Select(u => Path.GetFileNameWithoutExtension(u.AbsolutePath)).Where(n => n.StartsWith(prefix, StringComparison.Ordinal)).Order(StringComparer.Ordinal).ToArray();
        }
        catch (InvalidOperationException)
        {
            return [];
        }
    }

    private void LoadSettings()
    {
        var m = Draft.Meta;
        MapId = m.Id;
        MapName = m.Name;
        KindIndex = (int)m.Kind;
        (MapWidth, MapHeight) = (Draft.Width, Draft.Height);
        WallTexture = m.WallTexture;
        FloorTexture = m.FloorTexture;
        CeilingTexture = m.CeilingTexture ?? "(sky)";
        Music = m.Music;
        Dark = m.Dark;
        EncounterChance = m.EncounterChance;
        EncountersText = string.Join("\n", m.Encounters.Select(e => $"{e.Monster} {e.Count} {Math.Max(1, e.Weight)}"));
    }

    [RelayCommand]
    private void ApplySettings()
    {
        var m = Draft.Meta;
        m.Id = MapId.Trim();
        m.Name = MapName.Trim();
        m.Kind = (MapKind)Math.Clamp(KindIndex, 0, Kinds.Length - 1);
        m.WallTexture = WallTexture ?? m.WallTexture;
        m.FloorTexture = FloorTexture ?? m.FloorTexture;
        m.CeilingTexture = CeilingTexture is null or "(sky)" ? null : CeilingTexture;
        m.Music = Music ?? m.Music;
        m.Dark = Dark;
        m.EncounterChance = (int)Math.Clamp(EncounterChance ?? 0, 0, 50);
        var encounters = new List<EncounterEntryDef>();
        foreach (var line in EncountersText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            try
            {
                encounters.Add(new EncounterEntryDef
                {
                    Monster = parts[0],
                    Count = AVAMMB1.Core.Dice.DiceExpression.Parse(parts.Length > 1 ? parts[1] : "1"),
                    Weight = parts.Length > 2 && int.TryParse(parts[2], out var w) ? w : 1,
                });
            }
            catch (FormatException)
            {
                Status = Loc.F("Could not read the encounter line \"{0}\" (monster count weight, e.g. kobold 1d4 3).", line);
                return;
            }
        }
        m.Encounters = encounters;
        var (w2, h2) = ((int)(MapWidth ?? Draft.Width), (int)(MapHeight ?? Draft.Height));
        if (w2 != Draft.Width || h2 != Draft.Height)
        {
            Draft.Resize(w2, h2);
            (MapWidth, MapHeight) = (Draft.Width, Draft.Height);
            if (!Draft.InBounds(Start.X, Start.Y))
            {
                Start = new PixelPoint(0, 0);
            }
            RefreshCellEvents();
        }
        Status = Loc.T("Map settings applied.");
        Changed();
    }

    // ------------------------------------------------------------------ checking, files and test play

    /// <summary>What is wrong with the map (empty when it is ready).</summary>
    public ObservableCollection<string> Problems { get; } = new();

    /// <summary>Summary of the check.</summary>
    public string CheckText => Problems.Count == 0 ? Loc.T("No problems found.") : Loc.F("{0} problem(s):", Problems.Count);

    private void Check()
    {
        Problems.Clear();
        foreach (var p in Draft.Problems(_main.Services.Content, Start.X, Start.Y))
        {
            Problems.Add(p);
        }
        OnPropertyChanged(nameof(CheckText));
    }

    partial void OnStartChanged(PixelPoint value) => Check();

    /// <summary>Messages for the player.</summary>
    [ObservableProperty]
    private string _status = Loc.T("Pick a tool, then click the map. Near a square's side edits that side; its middle edits the square.");

    /// <summary>Mod pack folder name the map is saved into.</summary>
    [ObservableProperty]
    private string _packId = "my-maps";

    /// <summary>Whether saving adds a way in from an existing map (in the pack's mapPatches.json).</summary>
    [ObservableProperty]
    private bool _linkEntrance = true;

    /// <summary>The existing map with the way in.</summary>
    [ObservableProperty]
    private string _entranceMap = "brindlemoor";

    /// <summary>Where on that map.</summary>
    [ObservableProperty]
    private decimal? _entranceX = 13;

    /// <summary>Where on that map.</summary>
    [ObservableProperty]
    private decimal? _entranceY = 4;

    /// <summary>Party level for test plays.</summary>
    [ObservableProperty]
    private decimal? _testLevel = 3;

    /// <summary>Maps that can be opened (the game's and the packs' - a copy is edited).</summary>
    public string[] OpenableMaps { get; }

    /// <summary>The map chosen to open.</summary>
    [ObservableProperty]
    private string? _openMapId;

    /// <summary>The pack folder.</summary>
    public string PackFolder => Path.Combine(UserDataPaths.ModsDirectory, PackId.Trim());

    [RelayCommand]
    private void NewMap()
    {
        var kind = (MapKind)Math.Clamp(KindIndex, 0, Kinds.Length - 1);
        Draft = MapDraft.New("my_" + kind.ToString().ToLowerInvariant(), "My " + kind, kind, 12, 10);
        LoadSettings();
        Start = new PixelPoint(0, 0);
        Select(0, 0);
        _dirty = false;
        Changed();
        Status = Loc.T("A new map.");
    }

    [RelayCommand]
    private void OpenMap()
    {
        var id = OpenMapId;
        if (id is null)
        {
            return;
        }
        var file = Path.Combine(PackFolder, "Maps", id + ".json");
        try
        {
            Draft = File.Exists(file) ? MapDraft.FromJson(File.ReadAllText(file)) : MapDraft.From(_main.Services.Content.Map(id).Def);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or KeyNotFoundException)
        {
            Status = Loc.T("Could not open the map: ") + ex.Message;
            return;
        }
        LoadSettings();
        var arrival = Draft.Events.FirstOrDefault(e => e.Type == MapEventKind.Teleport);
        Start = arrival is null ? new PixelPoint(0, 0) : new PixelPoint(arrival.X, arrival.Y);
        Select(Start.X, Start.Y);
        _dirty = false;
        Version++;
        Check();
        Status = File.Exists(file)
            ? Loc.F("Opened {0} from the pack.", id)
            : Loc.F("Opened a copy of {0}. Saving it into a pack makes the pack's version replace the game's while the pack is on.", id);
    }

    [RelayCommand]
    private void Save()
    {
        ApplySettingsQuietly();
        if (Problems.Any(p => p.Contains("map id", StringComparison.Ordinal)) || string.IsNullOrWhiteSpace(PackId) || PackId.Any(ch => !(char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_')))
        {
            Status = Loc.T("Choose a map id and a pack name made of letters, digits, '-' and '_'.");
            return;
        }
        try
        {
            var entrance = LinkEntrance ? (EntranceMap, (int)(EntranceX ?? 0), (int)(EntranceY ?? 0)) : ((string, int, int)?)null;
            var path = MapPackWriter.Save(Draft, PackFolder, PackId.Trim(), entrance, (Start.X, Start.Y));
            _dirty = false;
            Status = Loc.F("Saved to {0}. Switch the pack on in Mods and restart to play it in the game.", path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Status = Loc.T("Could not save: ") + ex.Message;
        }
    }

    private void ApplySettingsQuietly()
    {
        var status = Status;
        ApplySettings();
        Status = status;
    }

    [RelayCommand]
    private void TestPlay()
    {
        ApplySettingsQuietly();
        if (!Draft.InBounds(Start.X, Start.Y))
        {
            Status = Loc.T("Choose where the party starts (the Start tool).");
            return;
        }
        try
        {
            _main.StartMapTest(this, Draft.ToMapDef(), Start.X, Start.Y, (int)Math.Clamp(TestLevel ?? 1, 1, Rulebook.MaxLevel));
        }
        catch (InvalidDataException ex)
        {
            Status = Loc.T("The map cannot be played yet: ") + ex.Message;
        }
    }

    /// <summary>Called when a test play ends.</summary>
    /// <param name="how">What happened.</param>
    public void TestPlayEnded(string how) => Status = how;

    [RelayCommand]
    private void Back()
    {
        if (_dirty)
        {
            _dirty = false;
            Status = Loc.T("There are unsaved changes - Back again leaves without saving.");
            return;
        }
        _main.ShowMods();
    }

    /// <inheritdoc />
    public override bool HandleKey(Key key)
    {
        if (key == Key.Escape)
        {
            BackCommand.Execute(null);
            return true;
        }
        return false;
    }
}
