using System.Numerics;
using AiBehaviorLab.Brains.Baseline;
using AiBehaviorLab.Rules;
using AiBehaviorLab.Simulation;
using static AiBehaviorLab.Tests.TestSupport;

namespace AiBehaviorLab.Tests;

public class CombatSimulationTests
{
    // 보스 (0,0)이 위(+Y)를 볼 때, 플레이어 (0,3)은 내려찍기 원(중심 (0,3), r2.5) 한가운데다.
    // 내려찍기 선딜 0.7초 = 21틱 → 틱 0에 시작하면 틱 21에 판정.

    [Fact]
    public void LightAttack_HitsAfterWindup()
    {
        var rules = MakeRules(player: Vector2.Zero, boss: new Vector2(0f, 2f));
        var sim = StartMatch(rules, ScriptedBrain.At(0, Intent.Use(ActionId.LightAttack, Up)), ScriptedBrain.Idle());

        sim.Steps(9); // 선딜 0.3초 = 9틱
        Assert.Equal(600, sim.Boss.Hp);

        sim.Step();
        Assert.Equal(595, sim.Boss.Hp);
        Assert.Contains(sim.Events, e => e is { Kind: CombatEventKind.Hit, Source: Side.Player, Value: 5 });
    }

    [Fact]
    public void LightAttack_MissesOutOfRange()
    {
        // 사거리 1.5 + 보스 몸 1.0 = 2.5보다 멀다.
        var rules = MakeRules(player: Vector2.Zero, boss: new Vector2(0f, 3.2f));
        var sim = StartMatch(rules, ScriptedBrain.At(0, Intent.Use(ActionId.LightAttack, Up)), ScriptedBrain.Idle());

        sim.Steps(30);
        Assert.Equal(600, sim.Boss.Hp);
    }

    [Fact]
    public void Slam_HitsOnTick21()
    {
        var rules = MakeRules(player: new Vector2(0f, 3f), boss: Vector2.Zero);
        var sim = StartMatch(rules, ScriptedBrain.Idle(), ScriptedBrain.At(0, Intent.Use(ActionId.BossSlam, Up)));

        sim.Steps(21);
        Assert.Equal(100, sim.Player.Hp);

        sim.Step();
        Assert.Equal(70, sim.Player.Hp);
    }

    [Fact]
    public void Roll_InvulnerabilityDodgesSlam()
    {
        var rules = MakeRules(player: new Vector2(0f, 3f), boss: Vector2.Zero);
        // 틱 19에 구르면 무적 19~26, 판정 틱 21에는 아직 범위 안이지만 무적.
        var sim = StartMatch(rules,
            ScriptedBrain.At(19, Intent.Use(ActionId.Roll, Right)),
            ScriptedBrain.At(0, Intent.Use(ActionId.BossSlam, Up)));

        sim.Steps(40);

        Assert.Equal(100, sim.Player.Hp);
        Assert.Equal(1, sim.PlayerStatistics.Dodges);
        Assert.Contains(sim.Events, e => e is { Kind: CombatEventKind.Dodged, Action: ActionId.BossSlam });
        Assert.InRange(sim.Player.Position.X, 2.99f, 3.01f); // 0.4초 동안 3m
    }

    [Fact]
    public void Roll_TooEarly_StillHit()
    {
        var rules = MakeRules(player: new Vector2(0f, 4f), boss: Vector2.Zero);
        // 틱 5에 보스 쪽으로 구르면 무적은 12에 끝나고, 보스 몸에 밀려 (0,1.4)에 서서 원 안에 남는다.
        var sim = StartMatch(rules,
            ScriptedBrain.At(5, Intent.Use(ActionId.Roll, -Up)),
            ScriptedBrain.At(0, Intent.Use(ActionId.BossSlam, Up)));

        sim.Steps(30);
        Assert.Equal(70, sim.Player.Hp);
    }

    [Fact]
    public void Roll_RejectedWithoutStamina()
    {
        var rules = MakeRules(player: new Vector2(0f, -5f), boss: new Vector2(0f, 4f));
        rules = rules with { Player = rules.Player with { MaxStamina = 20f } };
        var sim = StartMatch(rules, ScriptedBrain.At(0, Intent.Use(ActionId.Roll, Right)), ScriptedBrain.Idle());

        sim.Steps(5);
        Assert.Equal(1, sim.PlayerStatistics.RejectedIntents);
        Assert.Equal(new Vector2(0f, -5f), sim.Player.Position);
    }

    [Fact]
    public void Potion_HealsAfterWindup_ClampedToMax()
    {
        var rules = MakeRules(player: new Vector2(0f, 3f), boss: Vector2.Zero);
        var sim = StartMatch(rules,
            ScriptedBrain.At(25, Intent.Use(ActionId.Potion, Up)),
            ScriptedBrain.At(0, Intent.Use(ActionId.BossSlam, Up)));

        sim.Steps(25 + 30); // 틱 25 시작, 선딜 30틱 → 틱 55에 회복
        Assert.Equal(70, sim.Player.Hp);
        Assert.Equal(2, sim.Player.Potions);

        sim.Step();
        Assert.Equal(100, sim.Player.Hp);
        Assert.Contains(sim.Events, e => e is { Kind: CombatEventKind.Healed, Value: 30 });
    }

    [Fact]
    public void Potion_RejectedWhenNoneLeft()
    {
        var rules = MakeRules(player: new Vector2(0f, -5f), boss: new Vector2(0f, 4f));
        rules = rules with { Player = rules.Player with { Potions = 0 } };
        var sim = StartMatch(rules, ScriptedBrain.At(0, Intent.Use(ActionId.Potion, Up)), ScriptedBrain.Idle());

        sim.Steps(3);
        Assert.Equal(1, sim.PlayerStatistics.RejectedIntents);
        Assert.Null(sim.Player.Action);
    }

    [Fact]
    public void Player_CannotUseBossAction()
    {
        var rules = DefaultRules.V0();
        var sim = StartMatch(rules, ScriptedBrain.At(0, Intent.Use(ActionId.BossSlam, Up)), ScriptedBrain.Idle());

        sim.Step();
        Assert.Equal(1, sim.PlayerStatistics.RejectedIntents);
    }

    [Fact]
    public void DoubleSweep_HitsTwice()
    {
        var rules = MakeRules(player: new Vector2(0f, 2.5f), boss: Vector2.Zero);
        var sim = StartMatch(rules, ScriptedBrain.Idle(), ScriptedBrain.At(0, Intent.Use(ActionId.BossSweepDouble, Up)));

        sim.Steps(60);
        Assert.Equal(50, sim.Player.Hp);
        Assert.Equal(2, sim.Events.Count(e => e is { Kind: CombatEventKind.Hit, Source: Side.Boss }));
    }

    [Theory]
    [InlineData(ActionId.BossShockwave, 80)]
    [InlineData(ActionId.BossShockwaveRing, 100)] // 안쪽 2.5m 안에 몸 전체가 있으면 안전
    public void Shockwave_RingHasSafeCenter(ActionId pattern, int expectedHp)
    {
        var rules = MakeRules(player: new Vector2(0f, 1.5f), boss: Vector2.Zero);
        var sim = StartMatch(rules, ScriptedBrain.Idle(), ScriptedBrain.At(0, Intent.Use(pattern, Up)));

        sim.Steps(50);
        Assert.Equal(expectedHp, sim.Player.Hp);
    }

    [Fact]
    public void Charge_MovesAndHitsAlongLine()
    {
        var rules = MakeRules(player: new Vector2(0f, 6f), boss: Vector2.Zero);
        var sim = StartMatch(rules, ScriptedBrain.Idle(), ScriptedBrain.At(0, Intent.Use(ActionId.BossCharge, Up)));

        sim.Steps(24); // 선딜 0.8초
        Assert.Equal(Vector2.Zero, sim.Boss.Position);

        sim.Steps(20);
        Assert.Equal(80, sim.Player.Hp);
        Assert.True(sim.Boss.Position.Y > 7f, $"보스가 돌진하지 않았다: {sim.Boss.Position}");
    }

    [Fact]
    public void ChargeShort_ReaimsSecondStepTowardPlayer()
    {
        // 짧은 돌진(위로 4m) 뒤 휩쓸기는 플레이어 쪽으로 다시 조준한다.
        var rules = MakeRules(player: new Vector2(3f, 4f), boss: Vector2.Zero);
        var sim = StartMatch(rules, ScriptedBrain.Idle(), ScriptedBrain.At(0, Intent.Use(ActionId.BossChargeShort, Up)));

        sim.Steps(24 + 6 + 1); // 선딜, 돌진, 두 번째 단계 선딜 시작
        var action = sim.Boss.Action!.Value;
        Assert.Equal(1, action.StepIndex);
        Assert.Equal(ActionPhase.Windup, action.Phase);
        Assert.True(action.AimDirection.X > 0.9f, $"플레이어 쪽(오른쪽)을 보지 않는다: {action.AimDirection}");
    }

    [Fact]
    public void Telegraph_MatchesSlamArea()
    {
        var rules = MakeRules(player: new Vector2(0f, 3f), boss: Vector2.Zero);
        var sim = StartMatch(rules, ScriptedBrain.Idle(), ScriptedBrain.At(0, Intent.Use(ActionId.BossSlam, Up)));

        sim.Steps(5);
        var slam = sim.Boss.Action!.Value;
        Assert.Equal(ActionPhase.Windup, slam.Phase);
        Assert.True(Telegraph.Threatens(rules, slam, new Vector2(0f, 3f), 0.4f));
        Assert.False(Telegraph.Threatens(rules, slam, new Vector2(0f, 7f), 0.4f));
        Assert.False(Telegraph.Threatens(rules, slam, new Vector2(0f, -3f), 0.4f));
    }

    [Fact]
    public void ReactionDelay_ShowsOlderOpponentState()
    {
        var rules = DefaultRules.V0();
        var bossBrain = new ScriptedBrain(_ => Intent.Move(Right));
        var playerBrain = ScriptedBrain.Idle();
        var sim = StartMatch(rules, playerBrain, bossBrain, new HumanLimits(0.3, 0f, 0));

        sim.Steps(30);

        const int delay = 9; // 0.3초
        for (int t = 0; t < 30; t++)
        {
            var expected = bossBrain.Seen[System.Math.Max(0, t - delay)].Self.Position;
            Assert.Equal(expected, playerBrain.Seen[t].Opponent.Position);
        }
    }

    [Fact]
    public void Mistake_TwistsActionDirection()
    {
        var rules = DefaultRules.V0();
        var sim = StartMatch(rules, ScriptedBrain.At(0, Intent.Use(ActionId.Roll, Right)), ScriptedBrain.Idle(),
            new HumanLimits(0, 0f, 1.0));

        sim.Steps(15);
        Assert.Equal(1, sim.PlayerStatistics.Mistakes);
        Assert.True(MathF.Abs(sim.Player.Position.Y - rules.PlayerStart.Y) > 1e-4f);
    }

    [Fact]
    public void Arena_KeepsCombatantsInside()
    {
        var rules = DefaultRules.V0();
        var sim = StartMatch(rules, new ScriptedBrain(_ => Intent.Move(-Up)), ScriptedBrain.Idle());

        sim.Steps(300);
        Assert.True(sim.Player.Position.Length() <= rules.ArenaRadius - rules.Player.BodyRadius + 1e-4f);
    }

    [Fact]
    public void Bodies_DoNotOverlapWhileWalking()
    {
        var rules = DefaultRules.V0();
        var sim = StartMatch(rules, new ScriptedBrain(o => Intent.Move(o.Opponent.Position - o.Self.Position)),
            ScriptedBrain.Idle());

        sim.Steps(200);
        float distance = Vector2.Distance(sim.Player.Position, sim.Boss.Position);
        Assert.True(distance >= rules.Player.BodyRadius + rules.Boss.BodyRadius - 1e-4f);
    }

    [Fact]
    public void Timeout_BossWins()
    {
        var rules = DefaultRules.V0();
        var result = StartMatch(rules, ScriptedBrain.Idle(), ScriptedBrain.Idle()).Run();

        Assert.Equal(Side.Boss, result.Winner);
        Assert.Equal(EndReason.Timeout, result.Reason);
        Assert.Equal(5400, result.Ticks);
    }

    [Fact]
    public void SameSeed_SameMatch()
    {
        var rules = DefaultRules.V0();
        MatchResult Run() => StartMatch(rules, new BaselinePlayerBrain(), new BaselineBossBrain(),
            HumanLimits.Casual, seed: 42).Run();

        var a = Run();
        var b = Run();

        Assert.Equal(a.Events, b.Events);
        Assert.Equal((a.Winner, a.Ticks, a.PlayerHp, a.BossHp), (b.Winner, b.Ticks, b.PlayerHp, b.BossHp));
        Assert.True(a.Events.Count > 10, "기준선끼리 실제로 싸워야 한다");
    }

    [Fact]
    public void Baselines_FinishWithinTimeLimit()
    {
        var rules = DefaultRules.V0();
        for (ulong seed = 1; seed <= 20; seed++)
        {
            var result = StartMatch(rules, new BaselinePlayerBrain(), new BaselineBossBrain(),
                HumanLimits.Casual, seed).Run();
            Assert.InRange(result.Ticks, 1, rules.TimeLimitTicks);
        }
    }
}
