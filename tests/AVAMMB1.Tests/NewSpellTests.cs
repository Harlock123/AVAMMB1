using AVAMMB1.Core.Content;
using AVAMMB1.Core.Items;
using AVAMMB1.Core.Rules;
using AVAMMB1.Core.Session;
using AVAMMB1.Core.World;

namespace AVAMMB1.Tests;

/// <summary>Reveal Secrets, Levitate, Sense Minds and the seventh-circle tomes.</summary>
public class NewSpellTests
{
    private static (GameSession S, AVAMMB1.Core.Characters.Character Caster) Caster(string cls, int level)
    {
        var s = TestContent.StartedSession(seed: 8);
        var c = s.State.Party.First(p => p.Class == cls);
        c.Experience = Rulebook.XpForLevel(s.Content.Class(cls), level);
        while (s.Rules.LevelUp(c, s.Random) is not null) { }
        c.Sp = c.MaxSp;
        return (s, c);
    }

    [Fact]
    public void RevealSecrets_UncoversNearbySecretDoors()
    {
        var (s, sorcerer) = Caster("sorcerer", 8);
        var map = s.Content.Maps.Values.First(m => Enumerable.Range(0, m.Height).Any(y => Enumerable.Range(0, m.Width).Any(x => m.GetWall(x, y, Direction.North) == WallKind.SecretDoor)));
        var (x, y) = Enumerable.Range(0, map.Height).SelectMany(yy => Enumerable.Range(0, map.Width).Select(xx => (xx, yy))).First(c => map.GetWall(c.xx, c.yy, Direction.North) == WallKind.SecretDoor);
        (s.State.MapId, s.State.X, s.State.Y) = (map.Id, x, y);
        Assert.False(s.State.IsSecretFound(map.Id, x, y, Direction.North));
        var r = s.Spells.Cast(sorcerer, s.Content.Spell("s_reveal"), s.State, null, -1, null);
        Assert.True(r.Success);
        Assert.True(s.State.IsSecretFound(map.Id, x, y, Direction.North));
    }

    [Fact]
    public void Levitate_FloatsOverPits_AndWearsOff()
    {
        var (s, sorcerer) = Caster("sorcerer", 8);
        var pit = s.Content.Maps.Values.SelectMany(m => m.AllEvents.Select(e => (m, e))).First(x => x.e.Type == MapEventKind.Trap && x.e.Trap == TrapEffect.Pit);
        Assert.True(s.Spells.Cast(sorcerer, s.Content.Spell("s_levitate"), s.State, null, -1, null).Success);
        Assert.True(s.State.LevitateSteps >= 30);
        (s.State.MapId, s.State.X, s.State.Y) = (pit.m.Id, pit.e.X, pit.e.Y);
        var hp = s.State.Party.Sum(c => c.Hp);
        var walker = new Walker(s) { Overwhelm = true };
        // Step off and back onto the pit square.
        var back = Enum.GetValues<Direction>().First(d => s.CanPass(pit.e.X, pit.e.Y, d));
        while (s.State.Facing != back) s.TurnRight();
        s.Move(MoveKind.Forward);
        while (s.State.Facing != back.Opposite()) s.TurnRight();
        var r = s.Move(MoveKind.Forward);
        Assert.Contains(r.Messages, m => m.Text.Contains("floats over", StringComparison.Ordinal));
        Assert.Equal(pit.m.Id, s.State.MapId); // not dropped down the pit
        Assert.Equal(hp, s.State.Party.Sum(c => c.Hp));
    }

    [Fact]
    public void SenseMinds_TellsWhatWaitsAndWhere()
    {
        var (s, cleric) = Caster("cleric", 8);
        (s.State.MapId, s.State.X, s.State.Y) = ("cellars", 1, 14);
        var r = s.Spells.Cast(cleric, s.Content.Spell("c_sense"), s.State, null, -1, null);
        Assert.True(r.Success);
        Assert.Contains(r.Messages, m => m.Text.Contains("You sense", StringComparison.Ordinal) && m.Text.Contains("squares away", StringComparison.Ordinal));
    }

    [Fact]
    public void SeventhCircle_ComesFromTomes_AtCasterLevelThirteen()
    {
        Assert.Equal(7, Rulebook.TopSpellLevel);
        var (s, sorcerer) = Caster("sorcerer", 13);
        Assert.DoesNotContain(s.Rules.KnownSpells(sorcerer), sp => sp.Id == "s_starfall"); // tome-only
        sorcerer.Backpack.Add(new ItemInstance("tome_starfall"));
        var r = s.Spells.UseItem(sorcerer, sorcerer.Backpack.Count - 1, s.State, null, -1, null);
        Assert.True(r.Success);
        Assert.Contains(s.Rules.KnownSpells(sorcerer), sp => sp.Id == "s_starfall");

        var (t, young) = Caster("sorcerer", 11);
        young.Backpack.Add(new ItemInstance("tome_starfall"));
        Assert.False(t.Spells.UseItem(young, young.Backpack.Count - 1, t.State, null, -1, null).Success);

        Assert.Contains("tome_starfall", s.Content.Shops["wintermere_outfitter"].Stock);
        Assert.Contains("tome_divine", s.Content.Shops["wintermere_outfitter"].Stock);
    }
}
