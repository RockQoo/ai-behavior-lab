using System.Numerics;
using AiBehaviorLab.Brains;
using AiBehaviorLab.Brains.Fsm;
using AiBehaviorLab.Replay;
using AiBehaviorLab.Rules;
using AiBehaviorLab.Simulation;
using static AiBehaviorLab.Tests.TestSupport;

namespace AiBehaviorLab.Tests;

public class StateMachineTests
{
    private enum S
    {
        A,
        B,
        C,
    }

    private sealed class RecordingState(string name, List<string> log) : IState
    {
        public void Enter(in Observation observation) => log.Add($"enter {name}");
        public Intent Tick(in Observation observation)
        {
            log.Add($"tick {name}");
            return Intent.None;
        }
        public void Exit(in Observation observation) => log.Add($"exit {name}");
    }

    private static Observation At(int tick) => new(tick, DefaultRules.V0(), default, default);

    [Fact]
    public void EntersInitialState_ThenTransitions_InOrder()
    {
        var log = new List<string>();
        var machine = new StateMachine<S>(S.A)
            .State(S.A, new RecordingState("A", log))
            .State(S.B, new RecordingState("B", log))
            .On(S.A, S.B, "틱 2", o => o.Tick == 2);

        machine.Tick(At(0));
        machine.Tick(At(1));
        Assert.Equal(S.A, machine.Current);
        Assert.Equal(2, machine.TicksInState);

        machine.Tick(At(2));
        Assert.Equal(S.B, machine.Current);
        Assert.Equal(1, machine.TicksInState);
        Assert.Equal("틱 2", machine.LastTransition!.Reason);
        Assert.Equal(2, machine.LastTransitionTick);
        Assert.Equal(["enter A", "tick A", "tick A", "exit A", "enter B", "tick B"], log);
    }

    [Fact]
    public void AnyTransition_HasPriority_AndSkipsSelf()
    {
        var log = new List<string>();
        var machine = new StateMachine<S>(S.A)
            .State(S.A, new RecordingState("A", log))
            .State(S.B, new RecordingState("B", log))
            .State(S.C, new RecordingState("C", log))
            .On(S.A, S.B, "보통", _ => true)
            .FromAny(S.C, "인터럽트", _ => true);

        machine.Tick(At(0));
        Assert.Equal(S.C, machine.Current); // 인터럽트가 먼저

        machine.Tick(At(1));
        Assert.Equal(S.C, machine.Current); // 자기 자신으로는 다시 들어가지 않는다
        Assert.Equal(2, machine.TicksInState);
    }

    [Fact]
    public void OneTransitionPerTick()
    {
        var machine = new StateMachine<S>(S.A)
            .State(S.A, new DelegateState(_ => Intent.None))
            .State(S.B, new DelegateState(_ => Intent.None))
            .State(S.C, new DelegateState(_ => Intent.None))
            .On(S.A, S.B, "1", _ => true)
            .On(S.B, S.C, "2", _ => true);

        machine.Tick(At(0));
        Assert.Equal(S.B, machine.Current);
        machine.Tick(At(1));
        Assert.Equal(S.C, machine.Current);
    }

    [Fact]
    public void MissingState_Throws()
    {
        var machine = new StateMachine<S>(S.A);
        Assert.Throws<InvalidOperationException>(() => machine.Tick(At(0)));
    }
}

public class FsmBrainTests
{
    [Fact]
    public void Boss_UsesFixedRotation()
    {
        // 가만히 있는 플레이어 옆에서 고정 순서로 공격한다.
        var rules = MakeRules(player: new Vector2(0f, -2.5f), boss: Vector2.Zero);
        rules = rules with { Player = rules.Player with { MaxHp = 1000 } }; // 여섯 번 맞아도 버티게
        var sim = StartMatch(rules, ScriptedBrain.Idle(), new FsmBossBrain());
        while (!sim.IsOver && sim.Events.Count(e => e.Kind == CombatEventKind.ActionStarted) < 6)
            sim.Step();

        var started = sim.Events
            .Where(e => e is { Kind: CombatEventKind.ActionStarted, Source: Side.Boss })
            .Select(e => e.Action!.Value)
            .ToList();
        Assert.Equal(
            [ActionId.BossSlam, ActionId.BossSweep, ActionId.BossSlam, ActionId.BossShockwave, ActionId.BossSlam, ActionId.BossSweep],
            started);
    }

    [Fact]
    public void Boss_ChargesAfterLongChase()
    {
        // 보스가 느려서 오래 쫓아도 못 닿으면 돌진한다.
        var rules = DefaultRules.V0();
        rules = rules with { Boss = rules.Boss with { MoveSpeed = 0.5f } };
        var sim = StartMatch(rules, ScriptedBrain.Idle(), new FsmBossBrain());

        sim.Steps(rules.ToTicks(1.5) + 2);
        Assert.Equal(ActionId.BossCharge, sim.Boss.Action?.Id);
    }

    [Fact]
    public void Player_WalksThenRollsOutOfSlam()
    {
        var rules = MakeRules(player: new Vector2(0f, 3f), boss: Vector2.Zero);
        var sim = StartMatch(rules, new FsmPlayerBrain(),
            ScriptedBrain.At(0, Intent.Use(ActionId.BossSlam, Up)));

        sim.Steps(40);
        Assert.Equal(100, sim.Player.Hp);
        Assert.Contains(sim.Events, e => e is { Kind: CombatEventKind.ActionStarted, Source: Side.Player, Action: ActionId.Roll });
    }

    [Fact]
    public void Player_BacksOffWhenStaminaLow()
    {
        var rules = DefaultRules.V0();
        rules = rules with { Player = rules.Player with { MaxStamina = 30f } };
        var setup = new MatchSetup(1, new FsmPlayerBrain(), new IdleBrainForTest()) { RecordDecisions = true };
        var sim = new CombatSimulation(rules, setup);

        sim.Step();
        Assert.Equal("숨 고르기", sim.LastPlayerDecision!.State);
        Assert.Contains("스태미나 부족", sim.LastPlayerDecision.LastTransition);
    }

    [Fact]
    public void FsmVsFsm_IsDeterministic()
    {
        var rules = DefaultRules.V0();
        MatchResult Run() => StartMatch(rules, new FsmPlayerBrain(), new FsmBossBrain(), HumanLimits.Casual, seed: 9).Run();

        var a = Run();
        var b = Run();
        Assert.Equal(a.Events, b.Events);
    }

    [Fact]
    public void Replay_ContainsDecisionsOnlyForExplainableBrains()
    {
        var spec = new MatchSpec(ReplayCatalog.RulesV0, 5, ReplayCatalog.FsmPlayer, ReplayCatalog.BaselineBoss,
            HumanLimits.Casual, HumanLimits.None);
        var replay = ReplayRecorder.Record(spec);

        Assert.Null(replay.Frames[0].PlayerDecision);
        Assert.NotNull(replay.Frames[1].PlayerDecision);
        Assert.Null(replay.Frames[1].BossDecision);
        Assert.Contains(replay.Frames, f => f.PlayerDecision?.State == "회피");

        var loaded = ReplayFile.FromJson(replay.ToJson());
        Assert.Equal(replay.Frames[100].PlayerDecision!.State, loaded.Frames[100].PlayerDecision!.State);
        Assert.Equal(replay.Frames[100].PlayerDecision!.Notes, loaded.Frames[100].PlayerDecision!.Notes);
        Assert.True(ReplayVerifier.Verify(loaded).Matches);
    }

    [Fact]
    public void Player_DoesNotPunishOutOfRange() // Codex 리뷰 R2
    {
        // 보스가 반대쪽(위)을 내려찍고 후딜에 들어간다. 플레이어(아래 2.7m)는 사거리 밖이라 먼저 다가간 뒤에 친다.
        var rules = MakeRules(player: new Vector2(0f, -2.7f), boss: Vector2.Zero);
        var sim = StartMatch(rules, new FsmPlayerBrain(), ScriptedBrain.At(0, Intent.Use(ActionId.BossSlam, Up)));

        float? distanceAtSwing = null;
        for (int i = 0; i < 90 && distanceAtSwing is null; i++)
        {
            int before = sim.Events.Count;
            sim.Step();
            if (sim.Events.Skip(before).Any(e => e is { Kind: CombatEventKind.ActionStarted, Action: ActionId.LightAttack }))
                distanceAtSwing = Vector2.Distance(sim.Player.Position, sim.Boss.Position);
        }

        Assert.NotNull(distanceAtSwing); // 후딜에 반격은 한다
        Assert.True(distanceAtSwing <= 1.5f + rules.Boss.BodyRadius, $"사거리 밖에서 휘둘렀다: {distanceAtSwing}m");
        sim.Steps(12); // 선딜 9틱 + 판정
        Assert.Equal(595, sim.Boss.Hp); // 휘두른 평타는 맞는다
    }

    [Fact]
    public void Replay_RejectsDecisionWithoutNotes() // Codex 리뷰 R3
    {
        var spec = new MatchSpec(ReplayCatalog.RulesV0, 5, ReplayCatalog.FsmPlayer, ReplayCatalog.FsmBoss,
            HumanLimits.Casual, HumanLimits.None);
        string json = ReplayRecorder.Record(spec).ToJson();
        int notes = json.IndexOf("\"Notes\":[", StringComparison.Ordinal);
        int end = json.IndexOf(']', notes);
        string broken = json[..notes] + "\"Notes\":null" + json[(end + 1)..];

        Assert.Throws<InvalidDataException>(() => ReplayFile.FromJson(broken));
    }

    [Fact]
    public void Verify_DoesNotCollectDecisions() // Codex 리뷰 R1
    {
        var spec = new MatchSpec(ReplayCatalog.RulesV0, 5, ReplayCatalog.FsmPlayer, ReplayCatalog.FsmBoss,
            HumanLimits.Casual, HumanLimits.None);
        var simulation = ReplayRecorder.CreateSimulation(spec, ReplayCatalog.CreateRules(spec.RulesName));
        simulation.Steps(10);

        Assert.Null(simulation.LastPlayerDecision);
        Assert.Null(simulation.LastBossDecision);
    }

    private sealed class IdleBrainForTest : IBrain
    {
        public string Name => "Idle";
        public void Reset(BrainContext context)
        {
        }
        public Intent Decide(in Observation observation) => Intent.None;
    }
}
