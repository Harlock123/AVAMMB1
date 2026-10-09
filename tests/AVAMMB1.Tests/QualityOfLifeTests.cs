using AVAMMB1.Core.Combat;
using AVAMMB1.Core.Input;
using AVAMMB1.Core.Items;
using AVAMMB1.Core.Persistence;
using AVAMMB1.Core.Rules;
using AVAMMB1.Core.Session;

namespace AVAMMB1.Tests;

/// <summary>Automap notes, monster knowledge and auto-fight.</summary>
public class QualityOfLifeTests
{
    [Fact]
    public void MapNotes_AreSetClearedAndSaved()
    {
        var s = TestContent.StartedSession();
        s.State.SetNote("cellars", 3, 4, "  Locked door - need a key  ");
        Assert.Equal("Locked door - need a key", s.State.NoteAt("cellars", 3, 4));
        Assert.Null(s.State.NoteAt("cellars", 4, 3));
        s.State.KnownMonsters.Add("kobold");

        var loaded = SaveGameService.Deserialize(SaveGameService.Serialize(new SaveFile { State = s.State })).State;
        Assert.Equal("Locked door - need a key", loaded.NoteAt("cellars", 3, 4));
        Assert.Contains("kobold", loaded.KnownMonsters);

        s.State.SetNote("cellars", 3, 4, " ");
        Assert.Empty(s.State.MapNotes);
    }

    [Fact]
    public void DefeatedMonsters_BecomeKnown_AndTheirStatsAreDescribed()
    {
        var s = TestContent.StartedSession();
        var rat = CombatEngine.Spawn(s.Content.Monster("cellar_rat"), 1, s.Random).Single();
        Assert.Contains("not yet studied", MonsterLore.Describe(rat, known: false));

        s.StartCombat([rat], new StepResult());
        new Walker(s).Fight();
        Assert.Contains("cellar_rat", s.State.KnownMonsters);

        var lich = CombatEngine.Spawn(s.Content.Monster("crypt_lich"), 1, s.Random).Single();
        var info = MonsterLore.Describe(lich, known: true);
        Assert.Contains("level 10", info);
        Assert.Contains("undead", info);
        Assert.Contains("immune to cold, poison", info);
        Assert.Contains("resists magic 25%", info);
    }

    [Fact]
    public void AutoTactics_HealsTheBadlyHurt_AndWeaponsOnlyModeSavesAttackSpells()
    {
        var s = TestContent.StartedSession();
        foreach (var c in s.State.Party)
        {
            c.Experience = Rulebook.XpForLevel(s.Content.Class(c.Class), 5);
            while (s.Rules.LevelUp(c, s.Random) is not null) { }
        }
        s.StartCombat(CombatEngine.Spawn(s.Content.Monster("kobold"), 4, s.Random), new StepResult());
        var combat = s.Combat!;
        combat.Advance();
        var guard = 0;
        // Reach the sorcerer's turn (back rank, attack spells known).
        while (combat.ActiveCharacter is { } c && c.Class != "sorcerer" && guard++ < 20)
        {
            combat.Act(new CombatAction(CombatActionKind.Block));
        }
        var sorcerer = combat.ActiveCharacter!;
        Assert.Equal("sorcerer", sorcerer.Class);
        Assert.Equal(CombatActionKind.Cast, AutoTactics.Choose(s.Rules, s.Spells, combat, sorcerer, s.State.Party, offensiveSpells: true).Kind);
        Assert.NotEqual(CombatActionKind.Cast, AutoTactics.Choose(s.Rules, s.Spells, combat, sorcerer, s.State.Party, offensiveSpells: false).Kind);

        // A badly hurt ally and a healing potion: the potion is used on them.
        var hurt = s.State.Party[0];
        hurt.Hp = 1;
        sorcerer.Backpack.Add(new ItemInstance("potion_healing"));
        var heal = AutoTactics.Choose(s.Rules, s.Spells, combat, sorcerer, s.State.Party, offensiveSpells: false);
        Assert.Equal(CombatActionKind.UseItem, heal.Kind);
        Assert.Equal(0, heal.Ally);
    }

    [Fact]
    public void Gamepad_TriggersRepeatAndAutoFightInCombat()
    {
        Assert.Equal(CombatCommand.Repeat, GamepadMapping.Map(GamepadContext.Combat, GamepadButton.LeftTrigger).Combat);
        Assert.Equal(CombatCommand.AutoFight, GamepadMapping.Map(GamepadContext.Combat, GamepadButton.RightTrigger).Combat);
    }

    [Fact]
    public void Gamepad_ExploringButtonsCanBeRebound_StartAlwaysOpensTheMenu()
    {
        var pad = GamepadMapping.DefaultExploring();
        Assert.Equal(InputAction.Search, GamepadMapping.Map(GamepadContext.Exploring, GamepadButton.X, pad).Action);

        // Put Search on A: Interact loses A, and A now searches.
        var displaced = GamepadMapping.Rebind(pad, InputAction.Search, GamepadButton.A);
        Assert.Equal(InputAction.Interact, displaced);
        Assert.Equal(InputAction.Search, GamepadMapping.Map(GamepadContext.Exploring, GamepadButton.A, pad).Action);
        Assert.Null(GamepadMapping.Map(GamepadContext.Exploring, GamepadButton.X, pad).Action);

        pad.Clear();
        Assert.Equal(InputAction.Menu, GamepadMapping.Map(GamepadContext.Exploring, GamepadButton.Start, pad).Action);
        // Combat buttons are fixed.
        Assert.Equal(CombatCommand.Primary, GamepadMapping.Map(GamepadContext.Combat, GamepadButton.A, pad).Combat);
    }

    [Fact]
    public void Settings_NewOptionsAreNormalizedAndSaved()
    {
        var s = new GameSettings { TextScale = 500, ViewResolution = 512, GamepadBindings = new() };
        s.Normalize();
        Assert.Equal(130, s.TextScale);
        Assert.Equal(480, s.ViewResolution);
        Assert.Equal(GamepadMapping.DefaultExploring().Count, s.GamepadBindings.Count);

        var dir = Path.Combine(Path.GetTempPath(), "avammb1-settings-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new SettingsStore(Path.Combine(dir, "settings.json"));
            s.ColorTheme = "HighContrast";
            s.SmoothView = true;
            GamepadMapping.Rebind(s.GamepadBindings, InputAction.Rest, GamepadButton.Y);
            store.Save(s);
            var loaded = store.Load();
            Assert.Equal("HighContrast", loaded.ColorTheme);
            Assert.True(loaded.SmoothView);
            Assert.Equal(GamepadButton.Y, loaded.GamepadBindings[InputAction.Rest]);
            Assert.False(loaded.GamepadBindings.ContainsKey(InputAction.Characters));
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }
}
