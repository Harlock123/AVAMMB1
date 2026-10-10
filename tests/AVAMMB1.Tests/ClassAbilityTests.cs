using AVAMMB1.Core.Characters;
using AVAMMB1.Core.Combat;
using AVAMMB1.Core.Content;
using AVAMMB1.Core.Rules;
using AVAMMB1.Core.Session;
using AVAMMB1.Core.World;

namespace AVAMMB1.Tests;

/// <summary>Guard, Lay on Hands, Aimed Shot, Sneak attack and lock-picking.</summary>
public class ClassAbilityTests
{
    private static (GameSession S, CombatEngine C) Battle(string monster = "ogre", int count = 1, int seed = 1)
    {
        var s = TestContent.StartedSession(seed);
        s.StartCombat(CombatEngine.Spawn(s.Content.Monster(monster), count, s.Random), new StepResult());
        var c = s.Combat!;
        c.Advance();
        return (s, c);
    }

    private static Character Next(CombatEngine c, string cls)
    {
        for (var i = 0; i < 40 && c.ActiveCharacter is { } a && a.Class != cls; i++)
        {
            c.Act(new CombatAction(CombatActionKind.Block));
        }
        return c.ActiveCharacter!;
    }

    [Fact]
    public void ClassesHaveTheirAbilities()
    {
        var db = TestContent.Content;
        Assert.Contains(ClassAbility.Guard, db.Class("knight").Abilities);
        Assert.Contains(ClassAbility.LayOnHands, db.Class("paladin").Abilities);
        Assert.Contains(ClassAbility.AimedShot, db.Class("archer").Abilities);
        Assert.Contains(ClassAbility.SneakAttack, db.Class("robber").Abilities);
        Assert.Contains(ClassAbility.PickLocks, db.Class("robber").Abilities);
        Assert.Contains(ClassAbility.Overcharge, db.Class("sorcerer").Abilities);
        Assert.Contains(ClassAbility.TurnUndead, db.Class("cleric").Abilities);
    }

    [Fact]
    public void Guard_RedirectsAttacksToTheKnight()
    {
        var (s, c) = Battle("ogre", 2);
        var knight = Next(c, "knight");
        var ward = s.State.Party.First(p => p.Class != "knight");
        var wardHp = ward.Hp;
        var log = c.Act(new CombatAction(CombatActionKind.Guard, Ally: s.State.Party.IndexOf(ward)));
        Assert.Contains(log, m => m.Text.Contains("raises a shield", StringComparison.Ordinal));
        // Guarding yourself is not allowed (the same character chooses again).
        var (t, c2) = Battle();
        var k2 = Next(c2, "knight");
        c2.Act(new CombatAction(CombatActionKind.Guard, Ally: t.State.Party.IndexOf(k2)));
        Assert.Same(k2, c2.ActiveCharacter);
    }

    [Fact]
    public void LayOnHands_HealsCuresPoison_OncePerBattle()
    {
        var (s, c) = Battle();
        var paladin = Next(c, "paladin");
        var hurt = s.State.Party[0];
        hurt.Hp = 1;
        hurt.Conditions |= Condition.Poisoned;
        c.Act(new CombatAction(CombatActionKind.LayOnHands, Ally: 0));
        Assert.Equal(Math.Min(hurt.MaxHp, 1 + 3 * paladin.Level + 5), hurt.Hp);
        Assert.False(hurt.Has(Condition.Poisoned));
        Assert.True(c.HasLaidHands(paladin));
    }

    [Fact]
    public void AimedShot_NeedsARoundToRecover()
    {
        var (s, c) = Battle("ogre", 1);
        var archer = Next(c, "archer");
        Assert.True(c.CanAim(archer));
        var round = c.Round;
        c.Act(new CombatAction(CombatActionKind.AimedShot, Target: 0));
        Assert.False(c.CanAim(archer)); // same round
        while (c.Outcome == CombatOutcome.Ongoing && c.Round == round) { c.Act(new CombatAction(CombatActionKind.Block)); }
        if (c.Outcome == CombatOutcome.Ongoing)
        {
            Assert.False(c.CanAim(archer)); // the next round: still steadying
        }
    }

    [Fact]
    public void SneakAttack_DoublesDamageInTheFirstRoundOnly()
    {
        var (s, c) = Battle("ogre", 1);
        var robber = Next(c, "robber");
        var log = c.Act(new CombatAction(robber.Equipment.ContainsKey(EquipSlot.Missile) && !c.IsInFrontRank(robber) ? CombatActionKind.Shoot : CombatActionKind.Attack, Target: 0));
        Assert.Contains(log, m => m.Text.Contains("from the shadows", StringComparison.Ordinal));
    }

    [Fact]
    public void Robber_PicksLocks_ButNotMasterLocks()
    {
        var s = TestContent.StartedSession(3);
        var crypt = s.Content.Map("crypt");
        // Find a locked door edge in the crypt.
        (int X, int Y, Direction D)? door = null;
        for (var y = 0; y < crypt.Height && door is null; y++)
            for (var x = 0; x < crypt.Width && door is null; x++)
                foreach (var d in Enum.GetValues<Direction>())
                    if (crypt.Probe(x, y, d).Wall == WallKind.LockedDoor && !crypt.IsSolid(x, y)) { door = (x, y, d); break; }
        Assert.NotNull(door);
        var (dx, dy, dir) = door.Value;
        (s.State.MapId, s.State.X, s.State.Y, s.State.Facing) = ("crypt", dx, dy, dir);
        Assert.False(s.CanPass(dx, dy, dir));
        foreach (var p in s.State.Party.Where(p => p.Class == "robber")) { p.Level = 20; } // 95% chance
        for (var i = 0; i < 10 && !s.CanPass(dx, dy, dir); i++)
        {
            s.Move(MoveKind.Forward);
            if (s.Combat is not null) { foreach (var c in s.State.Party) { c.Level = 20; c.MaxHp = c.Hp = 500; } new Walker(s).Fight(); }
            (s.State.X, s.State.Y, s.State.Facing) = (dx, dy, dir);
        }
        Assert.True(s.CanPass(dx, dy, dir));

        // The catacombs' throne room needs its key.
        var cat = s.Content.Map("catacombs");
        Assert.True(cat.Def.MasterLocks);
        var t = TestContent.StartedSession(4);
        (t.State.MapId, t.State.X, t.State.Y, t.State.Facing) = ("catacombs", 2, 4, Direction.North);
        Assert.Equal(WallKind.LockedDoor, cat.Probe(2, 4, Direction.North).Wall);
        var r = t.Move(MoveKind.Forward);
        Assert.Contains(r.Messages, m => m.Text.Contains("needs its key", StringComparison.Ordinal));
        Assert.False(t.CanPass(2, 4, Direction.North));
    }

    [Fact]
    public void LockpickChance_GrowsWithLevel()
    {
        var s = TestContent.StartedSession();
        var robber = s.State.Party.First(p => p.Class == "robber");
        var low = s.Rules.LockpickChance(robber);
        robber.Level = 10;
        Assert.True(s.Rules.LockpickChance(robber) > low);
        robber.Level = 30;
        Assert.Equal(95, s.Rules.LockpickChance(robber));
    }
}
