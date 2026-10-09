using AVAMMB1.Core.Items;

namespace AVAMMB1.Tests;

public class LightTests
{
    [Fact]
    public void UsingATorch_GivesLight_OutsideCombat()
    {
        var s = TestContent.StartedSession();
        var c = s.State.Party[0];
        c.Backpack.Add(new ItemInstance("torch"));
        s.State.LightSteps = 0;
        var r = s.Spells.UseItem(c, c.Backpack.Count - 1, s.State, null, -1, null);
        Assert.True(r.Success);
        Assert.Equal(150, s.State.LightSteps);
        Assert.DoesNotContain(c.Backpack, i => i.ItemId == "torch");
    }

    [Theory]
    [InlineData("c_light", 200)]
    [InlineData("s_light", 250)]
    [InlineData("i_light", 400)]
    public void LightSpells_LastTheirStatedSteps(string spell, int steps)
    {
        var def = TestContent.Content.Spell(spell);
        Assert.Equal(steps, def.Magnitude);
        Assert.True(def.Description is null || def.Description.Length == 0 || def.Description.Contains($"{steps} steps"));
    }
}
