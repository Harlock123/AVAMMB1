using AVAMMB1.Core.Combat;
using AVAMMB1.Core.Rules;
using AVAMMB1.Core.Session;

namespace AVAMMB1.Tests;

/// <summary>Battle messages carry the visual effect the app flashes, and maps their weather.</summary>
public class EffectTests
{
    [Fact]
    public void Spells_TagTheirEffect()
    {
        var s = TestContent.StartedSession();
        var db = s.Content;
        var fire = db.Spells.Values.Where(sp => sp.Combat && sp.Element == Element.Fire).MinBy(sp => sp.Level)!;
        var heal = db.Spells.Values.First(sp => sp.Effect == EffectKind.Heal && sp.Level == 1);
        foreach (var spell in new[] { fire, heal })
        {
            var caster = s.State.Party.First(c => db.Class(c.Class).SpellSchool == spell.School);
            caster.Experience = Rulebook.XpForLevel(db.Class(caster.Class), 15);
            while (s.Rules.LevelUp(caster, s.Random) is not null) { }
            caster.Sp = caster.MaxSp;
            s.StartCombat(CombatEngine.Spawn(db.Monster("kobold"), 1, s.Random), new StepResult());
            var r = s.Spells.Cast(caster, spell, s.State, s.Combat, 0, s.Combat!.Monsters[0]);
            Assert.Equal(spell == heal ? "heal" : "fire", r.Messages[0].Effect);
            s.EndCombat();
        }
    }

    [Fact]
    public void ColdLands_Snow_AndTheMarshRains()
    {
        Assert.Equal("snow", TestContent.Content.Map("frostmark").Def.Weather);
        Assert.Equal("rain", TestContent.Content.Map("duskmere").Def.Weather);
        Assert.Null(TestContent.Content.Map("cellars").Def.Weather);
    }
}
