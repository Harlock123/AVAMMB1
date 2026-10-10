using AVAMMB1.Core.Combat;
using AVAMMB1.Core.Dice;
using AVAMMB1.Core.Rules;
using AVAMMB1.Core.Session;

namespace AVAMMB1.Tests;

/// <summary>Difficulty levels and survival mode.</summary>
public class DifficultyTests
{
    /// <summary>Takes one real step (turning until a way is open).</summary>
    private static StepResult Step(GameSession s)
    {
        for (var i = 0; i < 4; i++)
        {
            var r = s.Move(MoveKind.Forward);
            if (r.Moved)
            {
                return r;
            }
            s.TurnRight();
        }
        throw new InvalidOperationException("Boxed in.");
    }

    private static MonsterInstance Fight(Difficulty d)
    {
        var s = TestContent.StartedSession();
        s.State.Difficulty = d;
        var ogre = new MonsterInstance(s.Content.Monster("kobold"), 40);
        s.StartCombat([ogre], new StepResult());
        return ogre;
    }

    [Fact]
    public void Difficulty_ScalesMonsterHpAndDamage()
    {
        Assert.Equal(40, Fight(Difficulty.Normal).MaxHp);
        Assert.Equal(30, Fight(Difficulty.Easy).MaxHp);
        var hard = Fight(Difficulty.Hard);
        Assert.Equal(52, hard.MaxHp);
        Assert.Equal(52, hard.Hp);
        Assert.Equal(10, hard.ScaleDamage(8));
        Assert.Equal(6, Fight(Difficulty.Easy).ScaleDamage(8));
        Assert.Equal(1, Fight(Difficulty.Easy).ScaleDamage(1)); // a hit always hurts a little
        Assert.Equal(8, Fight(Difficulty.Normal).ScaleDamage(8));
    }

    [Fact]
    public void Difficulty_ScalesGold()
    {
        int GoldFor(Difficulty d)
        {
            var s = TestContent.StartedSession(seed: 7);
            s.State.Difficulty = d;
            var monsters = CombatEngine.Spawn(s.Content.Monster("kobold"), 6, s.Random).ToList();
            s.StartCombat(monsters, new StepResult());
            foreach (var m in s.Combat!.Monsters)
            {
                m.Hp = 0;
            }
            s.Combat.Advance();
            return s.Combat.Rewards!.Gold;
        }
        var normal = GoldFor(Difficulty.Normal);
        Assert.True(normal > 0);
        Assert.Equal(normal * 125 / 100, GoldFor(Difficulty.Easy));
        Assert.Equal(normal * 85 / 100, GoldFor(Difficulty.Hard));
    }

    [Fact]
    public void NewGame_UsesTheChosenDifficultyAndSurvival()
    {
        var s = new GameSession(TestContent.Content, new DefaultRandomSource(1)) { Difficulty = Difficulty.Hard, Survival = true };
        s.NewGame(TestContent.Content.Config.Premades.Select(s.Factory.CreatePremade));
        Assert.Equal(Difficulty.Hard, s.State.Difficulty);
        Assert.True(s.State.Survival);
    }

    [Fact]
    public void Survival_EatsDailyRations_AndHungerHurtsButNeverKills()
    {
        var s = TestContent.StartedSession();
        s.State.Survival = true;
        var hero = s.State.Party[0];
        hero.Food = 1;
        s.State.Minutes = s.State.LastMealMinutes + GameState.MinutesPerDay - 1;
        var r = Step(s);
        Assert.Equal(0, hero.Food);
        Assert.Contains(r.Messages, m => m.Text.Contains("daily rations", StringComparison.Ordinal));

        var hp = hero.Hp;
        s.State.Minutes = s.State.LastMealMinutes + GameState.MinutesPerDay - 1;
        r = Step(s);
        Assert.True(hero.Hp < hp);
        Assert.Contains(r.Messages, m => m.Text.Contains("Hunger", StringComparison.Ordinal) && m.Text.Contains(hero.Name, StringComparison.Ordinal));

        hero.Hp = 1;
        s.State.Minutes = s.State.LastMealMinutes + GameState.MinutesPerDay - 1;
        Step(s);
        Assert.Equal(1, hero.Hp);
        Assert.True(hero.IsAlive);
    }

    [Fact]
    public void WithoutSurvival_TravelEatsNothing_AndTurningItOnLaterStartsFed()
    {
        var s = TestContent.StartedSession();
        var food = s.State.Party.Select(c => c.Food).ToList();
        s.State.Minutes += 5 * GameState.MinutesPerDay;
        Step(s);
        Assert.Equal(food, s.State.Party.Select(c => c.Food));

        s.State.Survival = true; // switched on mid-game: no back-dated meals
        Step(s);
        Assert.Equal(food, s.State.Party.Select(c => c.Food));
    }

    /// <summary>
    /// Balance of survival mode: ten days on the road without camping costs each character one food a
    /// day; camping daily costs nothing extra (the rest is the day's meal). Either way a full pack lasts
    /// well over a week and refilling it costs little next to what a party earns.
    /// </summary>
    [Fact]
    public void Survival_FoodCostsStayModest()
    {
        var s = TestContent.StartedSession();
        s.State.Survival = true;
        var start = s.State.Party.Select(c => c.Food).ToList();
        for (var day = 0; day < 10; day++)
        {
            s.State.Minutes = s.State.LastMealMinutes + GameState.MinutesPerDay - 1;
            Step(s);
        }
        Assert.Equal(start.Select(f => f - 10), s.State.Party.Select(c => c.Food));

        var camper = TestContent.StartedSession();
        camper.State.Survival = true;
        var before = camper.State.Party.Select(c => c.Food).ToList();
        for (var day = 0; day < 3; day++)
        {
            camper.State.Minutes += GameState.MinutesPerDay - 9 * 60;
            Step(camper);
            camper.Rest();
        }
        // Three rests eat three meals; the daily ration never comes due on top of them.
        Assert.All(camper.State.Party.Zip(before), p => Assert.InRange(before[0] - p.First.Food, 3, 4));

        var tavern = s.Content.Map("brindlemoor").AllEvents.First(e => e.Type == AVAMMB1.Core.Content.MapEventKind.Tavern);
        // Refilling six empty packs (240 rations) costs less than the starting purse.
        Assert.InRange(s.Town.FoodCost(tavern), 1, 300);
    }

    [Fact]
    public void Hard_BossesAreTougher_ElitesCommoner_AndExperienceSlower()
    {
        var s = TestContent.StartedSession();
        s.State.Difficulty = Difficulty.Hard;
        var boss = new MonsterInstance(s.Content.Monster("rimefang"), 100);
        var grunt = new MonsterInstance(s.Content.Monster("kobold"), 100);
        s.StartCombat([boss, grunt], new StepResult());
        Assert.Equal(140, boss.MaxHp);
        Assert.Equal(130, grunt.MaxHp);
        Assert.Equal(10, DifficultyRules.EliteChance(Difficulty.Hard));
        Assert.Equal(5, DifficultyRules.EliteChance(Difficulty.Normal));

        int XpFor(Difficulty d)
        {
            var t = TestContent.StartedSession(seed: 3);
            t.State.Difficulty = d;
            t.StartCombat(CombatEngine.Spawn(t.Content.Monster("kobold"), 4, t.Random), new StepResult());
            foreach (var m in t.Combat!.Monsters) m.Hp = 0;
            t.Combat.Advance();
            return t.Combat.Rewards!.Experience;
        }
        Assert.Equal(XpFor(Difficulty.Normal) * 90 / 100, XpFor(Difficulty.Hard));
        Assert.Equal(XpFor(Difficulty.Normal), XpFor(Difficulty.Easy));
    }
}
