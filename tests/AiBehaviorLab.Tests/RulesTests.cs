using System.Numerics;
using AiBehaviorLab.Math;
using AiBehaviorLab.Rules;

namespace AiBehaviorLab.Tests;

public class RulesTests
{
    [Theory]
    [InlineData(0.25, 8)] // 7.5 → 올림
    [InlineData(0.15, 5)] // 4.5 → 올림
    [InlineData(0.3, 9)]
    [InlineData(180, 5400)]
    public void ToTicks_RoundsHalfUp(double seconds, int ticks)
    {
        Assert.Equal(ticks, CombatRules.ToTicks(seconds, 30));
    }

    [Fact]
    public void V0_IsValid()
    {
        var rules = DefaultRules.V0();
        rules.Validate();
        Assert.Equal(Enum.GetValues<ActionId>().Length, rules.Actions.Count);
    }

    [Fact]
    public void Validate_RejectsZeroActiveTicks()
    {
        var rules = DefaultRules.V0();
        var broken = rules.Get(ActionId.LightAttack) with { Steps = [new ActionStep(5, 0)] };
        var actions = rules.Actions.ToDictionary(p => p.Key, p => p.Value);
        actions[ActionId.LightAttack] = broken;

        Assert.Throws<InvalidOperationException>(() => (rules with { Actions = actions }).Validate());
    }

    [Fact]
    public void Sector_RespectsAngle()
    {
        var sector = new SectorShape(3f, 90f);
        var up = new Vector2(0f, 1f);
        Assert.True(sector.Hits(Vector2.Zero, up, new Vector2(0f, 2f), 0.1f));
        Assert.True(sector.Hits(Vector2.Zero, up, new Vector2(1f, 1.5f), 0.1f));   // 약 34°
        Assert.False(sector.Hits(Vector2.Zero, up, new Vector2(2f, 0.5f), 0.1f));  // 약 76°
        Assert.False(sector.Hits(Vector2.Zero, up, new Vector2(0f, -2f), 0.1f));
    }

    [Fact]
    public void Line_RespectsWidthAndLength()
    {
        var line = new LineShape(8f, 2f);
        var up = new Vector2(0f, 1f);
        Assert.True(line.Hits(Vector2.Zero, up, new Vector2(0.9f, 5f), 0.4f));
        Assert.False(line.Hits(Vector2.Zero, up, new Vector2(1.5f, 5f), 0.4f));
        Assert.False(line.Hits(Vector2.Zero, up, new Vector2(0f, 8.5f), 0.4f));
    }

    [Fact]
    public void Random_SameSeedSameSequence()
    {
        var a = new DeterministicRandom(123);
        var b = new DeterministicRandom(123);
        for (int i = 0; i < 100; i++)
            Assert.Equal(a.NextULong(), b.NextULong());

        Assert.NotEqual(DeterministicRandom.Derive(1, 1), DeterministicRandom.Derive(1, 2));
    }
}
