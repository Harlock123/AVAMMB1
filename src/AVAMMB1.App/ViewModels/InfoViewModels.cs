using Avalonia.Input;
using AVAMMB1.Core.Info;
using AVAMMB1.Core.Input;
using AVAMMB1.Core.Persistence;
using CommunityToolkit.Mvvm.Input;

namespace AVAMMB1.App.ViewModels;

/// <summary>Release notes ("What's new"), shown once after an update or from the title screen.</summary>
/// <param name="entries">Releases to show, newest first.</param>
/// <param name="onClose">Called when the player continues.</param>
public sealed partial class WhatsNewViewModel(IReadOnlyList<ReleaseEntry> entries, Action onClose) : ViewModelBase
{
    /// <summary>Releases.</summary>
    public IReadOnlyList<ReleaseView> Releases { get; } = entries.Select(e => new ReleaseView(e)).ToList();

    /// <summary>Heading.</summary>
    public string Title => Releases.Count == 1 ? $"What's new in AVAM&M {Releases[0].Version}" : "What's new";

    [RelayCommand]
    private void Close() => onClose();

    /// <inheritdoc />
    public override bool HandleKey(Key key)
    {
        if (key is Key.Escape or Key.Enter or Key.Space)
        {
            Close();
            return true;
        }
        return false;
    }
}

/// <summary>One release in the notes list.</summary>
/// <param name="Entry">Release.</param>
public sealed record ReleaseView(ReleaseEntry Entry)
{
    /// <summary>Version text.</summary>
    public string Version => Entry.Version.ToString();
    /// <summary>Heading.</summary>
    public string Heading => $"Version {Entry.Version}" + (Entry.Date.Length > 0 ? $"  -  {Entry.Date}" : "");
    /// <summary>Lines; section names (Added, Changed...) are the ones without a bullet.</summary>
    public IReadOnlyList<NoteLine> Lines => Entry.Lines.Select(l => new NoteLine(l)).ToList();
}

/// <summary>A release-notes line.</summary>
/// <param name="Text">Text.</param>
public sealed record NoteLine(string Text)
{
    /// <summary>Whether this is a section heading.</summary>
    public bool IsHeading => !Text.TrimStart().StartsWith('•');
    /// <summary>Indent for nested bullets.</summary>
    public Avalonia.Thickness Indent => new(IsHeading ? 0 : 8 + 12 * (Text.Length - Text.TrimStart().Length) / 2, IsHeading ? 8 : 1, 0, 1);
    /// <summary>Headings are bold.</summary>
    public Avalonia.Media.FontWeight Weight => IsHeading ? Avalonia.Media.FontWeight.Bold : Avalonia.Media.FontWeight.Normal;
    /// <summary>Text without leading spaces.</summary>
    public string Shown => Text.TrimStart();
}

/// <summary>Help: how to play, plus the current keyboard and controller controls.</summary>
public sealed partial class HelpViewModel : ViewModelBase
{
    private readonly Action _onClose;

    /// <summary>Creates the screen.</summary>
    /// <param name="settings">Settings (for the current bindings).</param>
    /// <param name="onClose">Called on close.</param>
    public HelpViewModel(GameSettings settings, Action onClose)
    {
        _onClose = onClose;
        Keys = Enum.GetValues<InputAction>()
            .Select(a => new HelpRow(PadRow.LabelOf(a), settings.KeyBindings.TryGetValue(a, out var k) && k.Count > 0 ? string.Join(", ", k) : "-"))
            .Concat(CombatKeys)
            .ToList();
        Pad = Enum.GetValues<InputAction>()
            .Where(a => settings.GamepadBindings.ContainsKey(a))
            .Select(a => new HelpRow(PadRow.LabelOf(a), PadRow.Name(settings.GamepadBindings[a])))
            .Append(new HelpRow("Menu (always)", "Start"))
            .Concat(CombatPad)
            .ToList();
    }

    private static readonly HelpRow[] CombatKeys =
    [
        new("Battle: fight / run / bribe", "F, R, B (before the first round)"),
        new("Battle: attack, shoot, cast, item, block, run", "A, S, C, U, B, R"),
        new("Battle: repeat last actions / auto-fight", "E, O"),
        new("Battle: class ability (guard / lay on hands / aimed shot)", "G, L, T"),
        new("Battle: choose target or ally", "1-9 or click"),
        new("Battle: choose spell or item (keys shown in the list)", "1-9, then A-Z"),
        new("Fullscreen", "F11 or Alt+Enter"),
    ];

    private static readonly HelpRow[] CombatPad =
    [
        new("Battle: fight / attack / continue", "A"),
        new("Battle: cast, item, block, run", "X, Y, B, View"),
        new("Battle: change target", "LB or left / right"),
        new("Battle: class ability", "RB"),
        new("Battle: repeat last actions / auto-fight", "LT / RT"),
        new("Menus", "D-pad moves, A selects, B back"),
    ];

    /// <summary>Keyboard controls.</summary>
    public IReadOnlyList<HelpRow> Keys { get; }
    /// <summary>Controller controls.</summary>
    public IReadOnlyList<HelpRow> Pad { get; }

    /// <summary>How-to-play sections.</summary>
    public IReadOnlyList<HelpRow> Basics { get; } =
    [
        new("Your party", "Up to six adventurers. Knights, paladins and archers fight best in the front three places; sorcerers and clerics cast from the back. Drag a party card along the bar at the bottom to change places (not in battle), or reorder at an inn."),
        new("Exploring", "Move square by square. Step onto signs, shops and stairs, or press Use to interact with what is in front of you. The automap (M) remembers only what you have seen; press N to note a square."),
        new("Quests", "Talk to the people in towns. The journal (J) keeps track of every quest you have heard of, and the Clues tab keeps the signs and warnings you have read."),
        new("Fighting", "Choose an action for each character in turn. Monsters you have beaten before show their strengths and weaknesses when targeted. Repeat (E) replays everyone's last action; Auto (O) fights for you until someone is badly hurt."),
        new("Class abilities", "Knights can Guard (G) a companion for the round - blows aimed at them strike the knight instead. Paladins can Lay on Hands (L) once per battle, healing and drawing out poison. Archers can take an Aimed shot (T): +4 to hit for double damage, though not two rounds running. Robbers strike from the shadows for double damage in the first round of a battle, and try to pick locked doors when you walk into them - though some locks need their key."),
        new("Light", "Dungeons are dark, and the better your light the further you see (and map): a Torch lights 5 squares ahead for 150 steps (5 gold), Holy Light (cleric, 200 steps), Glowlight (sorcerer, 250) and a Scroll of Light (400) light 6. To light a torch, open a character's sheet (I), pick it in the backpack and press Use. Squares of magical darkness stay dark whatever you carry, and you cannot light a torch in battle."),
        new("Lanterns and oil", "A Brass Lantern (150 gold) is the brightest light - 8 squares, with a warm glow. Equip it in the Light slot on any character's sheet (select it in the backpack and press Equip or Use); it lights the way for the whole party and burns oil only in dark places (1500 steps when full). The counter at the top right shows the oil left, and you are warned when it runs low. Use a Flask of Oil (4 gold) to add 750 steps - it fills an equipped lantern, or one still in a backpack; a lantern is bought full, so keep flasks until it burns down - or throw one in battle for 2d6 fire damage. Somewhere in the north lies a lantern that never needs oil."),
        new("Day and night", "Time passes as you travel - three minutes a step, eight hours a rest - and the clock is shown under your gold. From 20:00 to 05:00 it is night: under the open sky you see only 4 squares unless your light reaches further (a lantern burns outdoors at night, though not in town), more monsters are about - some only come out after dark - and elites are twice as common. Shops, training grounds and academies close for the night; inns, temples and taverns stay open, and a night at the inn lasts until 07:00."),
        new("Resting and healing", "Rest (R) in a quiet spot to recover. Food is eaten only when you rest - 1 unit per character each time (travelling and nights at an inn use none); a character with no food cannot recover. Buy food at a tavern; the party cards show what each carries. Poison stops healing and disease halves it - cure them with spells, cure potions or at a temple, which also raises the dead."),
        new("Getting stronger", "Experience is not enough on its own: pay to train at a training ground to gain a level. Buy better equipment in shops and sell what you do not need."),
        new("Gold", "Loot goes to the party purse. Each character can also carry gold of their own; the character sheet moves gold between them and the purse."),
        new("Danger", "Dungeons hide spinners that turn you around, dark and anti-magic squares, traps and secret doors (search with F). Save often (F5), especially before a boss."),
    ];

    [RelayCommand]
    private void Close() => _onClose();

    /// <inheritdoc />
    public override bool HandleKey(Key key)
    {
        if (key is Key.Escape or Key.F1 or Key.H)
        {
            Close();
            return true;
        }
        return false;
    }
}

/// <summary>A two-column help line.</summary>
/// <param name="Name">What.</param>
/// <param name="Value">How.</param>
public sealed record HelpRow(string Name, string Value);
