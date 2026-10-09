using AVAMMB1.Core.Combat;
using AVAMMB1.Core.Content;
using AVAMMB1.Core.Items;
using AVAMMB1.Core.Rules;
using AVAMMB1.Core.Session;
using AVAMMB1.Core.World;

namespace AVAMMB1.Tests;

/// <summary>Spinners, magical darkness, anti-magic squares and the new trap types.</summary>
public class HazardTests
{
    private static (int X, int Y) FindEvent(string mapId, MapEventKind kind, Func<MapEventDef, bool>? filter = null)
    {
        var e = TestContent.Content.Map(mapId).AllEvents.First(e => e.Type == kind && (filter?.Invoke(e) ?? true));
        return (e.X, e.Y);
    }

    private static GameSession At(string mapId, int x, int y, Direction facing, int seed = 3)
    {
        var s = TestContent.StartedSession(seed);
        s.State.MapId = mapId;
        s.State.X = x;
        s.State.Y = y;
        s.State.Facing = facing;
        return s;
    }

    /// <summary>Walks onto a cell from an open neighbour.</summary>
    private static StepResult StepOnto(GameSession s, int x, int y)
    {
        foreach (var d in Enum.GetValues<Direction>())
        {
            var from = (X: x - d.Dx(), Y: y - d.Dy());
            if (s.CurrentMap.InBounds(from.X, from.Y) && !s.CurrentMap.IsSolid(from.X, from.Y))
            {
                s.State.X = from.X;
                s.State.Y = from.Y;
                if (s.CanPass(from.X, from.Y, d))
                {
                    s.State.Facing = d;
                    return s.Move(MoveKind.Forward);
                }
            }
        }
        throw new InvalidOperationException($"No way onto ({x},{y}).");
    }

    [Fact]
    public void Content_HasEveryHazard()
    {
        var db = TestContent.Content;
        var events = db.Maps.Values.SelectMany(m => m.AllEvents).ToList();
        Assert.True(events.Count(e => e.Type == MapEventKind.Spinner) >= 3);
        foreach (var effect in Enum.GetValues<TrapEffect>().Where(t => t != TrapEffect.Damage))
        {
            Assert.Contains(events, e => e.Type == MapEventKind.Trap && e.Trap == effect);
        }
        var crypt = db.Map("crypt");
        Assert.Contains(Enumerable.Range(0, 16).SelectMany(y => Enumerable.Range(0, 16).Select(x => (x, y))), c => crypt.IsDarkness(c.x, c.y));
        var vault = db.Map("vault");
        Assert.Contains(Enumerable.Range(0, 12).SelectMany(y => Enumerable.Range(0, 12).Select(x => (x, y))), c => vault.IsAntiMagic(c.x, c.y));
    }

    [Fact]
    public void Spinner_TurnsThePartySilently()
    {
        var (x, y) = FindEvent("cellars", MapEventKind.Spinner);
        var facings = new HashSet<Direction>();
        for (var seed = 0; seed < 20; seed++)
        {
            var s = At("cellars", x, y, Direction.North, seed);
            var r = StepOnto(s, x, y);
            Assert.True(r.Moved);
            Assert.Equal((x, y), (s.State.X, s.State.Y));
            Assert.DoesNotContain(r.Messages, m => m.Kind == MessageKind.Story);
            facings.Add(s.State.Facing);
        }
        Assert.True(facings.Count >= 3, "spinner should leave the party facing various directions");
    }

    [Fact]
    public void MagicalDarkness_DefeatsLight()
    {
        var crypt = TestContent.Content.Map("crypt");
        var dark = Enumerable.Range(0, 16).SelectMany(y => Enumerable.Range(0, 16).Select(x => (x, y))).First(c => crypt.IsDarkness(c.x, c.y));
        var s = At("crypt", dark.x, dark.y, Direction.North);
        s.State.LightSteps = 500;
        Assert.True(s.IsDarkHere);
        Assert.Equal(1, s.ViewDistance);
    }

    [Theory]
    [InlineData("crypt", "darkness")]
    [InlineData("vault", "magic falls silent")]
    public void EnteringZones_IsAnnounced(string mapId, string expected)
    {
        var map = TestContent.Content.Map(mapId);
        bool InZone(int x, int y) => mapId == "crypt" ? map.IsDarkness(x, y) : map.IsAntiMagic(x, y);
        var cells = Enumerable.Range(0, map.Height).SelectMany(y => Enumerable.Range(0, map.Width).Select(x => (x, y)));
        foreach (var (x, y) in cells.Where(c => InZone(c.x, c.y)))
        {
            foreach (var d in Enum.GetValues<Direction>())
            {
                var from = (X: x - d.Dx(), Y: y - d.Dy());
                if (!map.InBounds(from.X, from.Y) || InZone(from.X, from.Y) || map.EventsAt(x, y).Count > 0)
                {
                    continue;
                }
                var s = At(mapId, from.X, from.Y, d);
                if (!s.CanPass(from.X, from.Y, d))
                {
                    continue;
                }
                var r = s.Move(MoveKind.Forward);
                Assert.True(r.Moved);
                Assert.Contains(r.Messages, m => m.Text.Contains(expected, StringComparison.OrdinalIgnoreCase));
                return;
            }
        }
        Assert.Fail("no entrance into the zone found");
    }

    [Fact]
    public void AntiMagic_BlocksSpellsScrollsAndWands_ButNotPotions()
    {
        var vault = TestContent.Content.Map("vault");
        var spot = Enumerable.Range(0, 12).SelectMany(y => Enumerable.Range(0, 12).Select(x => (x, y))).First(c => vault.IsAntiMagic(c.x, c.y));
        var s = At("vault", spot.x, spot.y, Direction.North);
        var mirela = s.State.Party.Single(c => c.Class == "cleric");
        var light = s.Content.Spell("c_light");
        Assert.Equal(AVAMMB1.Core.Magic.SpellCaster.SuppressedMessage, s.Spells.CanCast(mirela, light, inCombat: false));
        Assert.False(s.Spells.Cast(mirela, light, s.State, null, -1, null).Success);

        mirela.Backpack.Add(new ItemInstance("scroll_light"));
        Assert.False(s.Spells.UseItem(mirela, mirela.Backpack.Count - 1, s.State, null, 0, null).Success);
        mirela.Hp = 1;
        mirela.Backpack.Add(new ItemInstance("potion_healing"));
        var potion = s.Spells.UseItem(mirela, mirela.Backpack.Count - 1, s.State, null, s.State.Party.IndexOf(mirela), null);
        Assert.True(potion.Success, string.Join(" | ", potion.Messages.Select(m => m.Text)));
        Assert.True(mirela.Hp > 1);

        // Outside the zone magic works again.
        s.State.MapId = "vault";
        s.State.X = 5;
        s.State.Y = 11;
        Assert.Null(s.Spells.CanCast(mirela, light, inCombat: false));
    }

    [Fact]
    public void AntiMagic_SuppressesMonsterAbilitiesInBattle()
    {
        var vault = TestContent.Content.Map("vault");
        var spot = Enumerable.Range(0, 12).SelectMany(y => Enumerable.Range(0, 12).Select(x => (x, y))).First(c => vault.IsAntiMagic(c.x, c.y));
        var s = At("vault", spot.x, spot.y, Direction.North);
        var start = new StepResult();
        s.StartCombat(CombatEngine.Spawn(s.Content.Monster("necromancer"), 3, s.Random), start);
        var combat = s.Combat!;
        var ysolde = s.State.Party.Single(c => c.Class == "sorcerer");
        Assert.Equal(AVAMMB1.Core.Magic.SpellCaster.SuppressedMessage, s.Spells.CanCast(ysolde, s.Content.Spell("s_spark"), inCombat: true));
        var log = new List<AVAMMB1.Core.Session.GameMessage>();
        log.AddRange(combat.Advance());
        for (var i = 0; i < 40 && combat.Outcome == CombatOutcome.Ongoing; i++)
        {
            log.AddRange(combat.Act(new CombatAction(CombatActionKind.Block)));
        }
        Assert.DoesNotContain(log, m => m.Text.Contains(" uses ", StringComparison.Ordinal));
    }

    [Fact]
    public void TeleportTrap_MovesThePartyToAnotherReachableSquare()
    {
        var (x, y) = FindEvent("crypt", MapEventKind.Trap, e => e.Trap == TrapEffect.Teleport);
        var moved = 0;
        for (var seed = 0; seed < 10; seed++)
        {
            var s = At("crypt", x, y, Direction.North, seed);
            s.State.Party.ForEach(c => c.Stats[Stat.Speed] = 3); // poor thieves: the trap fires
            var r = StepOnto(s, x, y);
            if ((s.State.X, s.State.Y) != (x, y))
            {
                moved++;
                Assert.Equal("crypt", s.State.MapId);
                Assert.False(s.CurrentMap.IsSolid(s.State.X, s.State.Y));
                Assert.Contains(r.Messages, m => m.Text.Contains("somewhere else", StringComparison.Ordinal));
            }
        }
        Assert.True(moved >= 5, $"teleport fired only {moved}/10 times");
    }

    [Fact]
    public void PitTrap_HurtsAndDropsTheParty()
    {
        var (x, y) = FindEvent("crypt", MapEventKind.Trap, e => e.Trap == TrapEffect.Pit);
        for (var seed = 0; seed < 10; seed++)
        {
            var s = At("crypt", x, y, Direction.North, seed);
            var hp = s.State.Party.Sum(c => c.Hp);
            StepOnto(s, x, y);
            if ((s.State.X, s.State.Y) != (x, y))
            {
                Assert.True(s.State.Party.Sum(c => c.Hp) < hp);
                return;
            }
        }
        Assert.Fail("the pit never fired");
    }

    [Fact]
    public void AlarmTrap_StartsABattleWithItsMonsters()
    {
        var (x, y) = FindEvent("crypt", MapEventKind.Trap, e => e.Trap == TrapEffect.Alarm);
        for (var seed = 0; seed < 10; seed++)
        {
            var s = At("crypt", x, y, Direction.North, seed);
            var r = StepOnto(s, x, y);
            if (r.CombatStarted)
            {
                Assert.Contains(s.Combat!.Monsters, m => m.Def.Id == "zombie");
                return;
            }
        }
        Assert.Fail("the alarm never fired");
    }

    [Fact]
    public void SleepingGas_NeverLeavesThePartyStuck()
    {
        var (x, y) = FindEvent("cellars", MapEventKind.Trap, e => e.Conditions.HasFlag(Condition.Asleep));
        for (var seed = 0; seed < 20; seed++)
        {
            var s = At("cellars", x, y, Direction.North, seed);
            s.State.Party.ForEach(c => c.Stats[Stat.Luck] = 3); // fail the saving throws
            var r = StepOnto(s, x, y);
            if (r.CombatStarted)
            {
                Assert.NotNull(s.Combat); // ambushed while asleep: sleepers wake during the battle
            }
            else
            {
                Assert.All(s.State.Party, c => Assert.False(c.Has(Condition.Asleep)));
                Assert.Contains(s.State.Party, c => c.CanAct);
            }
        }
    }
}
