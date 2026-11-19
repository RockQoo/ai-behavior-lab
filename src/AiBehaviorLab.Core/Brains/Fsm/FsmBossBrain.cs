using System.Numerics;
using AiBehaviorLab.Math;
using AiBehaviorLab.Rules;
using AiBehaviorLab.Simulation;

namespace AiBehaviorLab.Brains.Fsm;

/// <summary>
/// FSM 보스의 매개변수. 난수를 쓰지 않으므로 같은 상황이면 늘 같은 순서로 공격한다(외울 수 있는 고정 보스, 실험 1-A).
/// </summary>
public sealed record FsmBossParameters
{
    /// <summary>근접 패턴 순환 순서. 끝나면 처음으로 돌아간다.</summary>
    public IReadOnlyList<ActionId> Rotation { get; init; } =
        [ActionId.BossSlam, ActionId.BossSweep, ActionId.BossSlam, ActionId.BossShockwave];

    public float CloseRange { get; init; } = 3.5f;
    public float ChargeMinRange { get; init; } = 5f;
    public float ChargeMaxRange { get; init; } = 9f;

    /// <summary>이만큼 쫓아도 사거리에 못 들어오면 돌진한다.</summary>
    public double ChaseSecondsBeforeCharge { get; init; } = 1.5;

    /// <summary>공격이 끝난 뒤 쉬는 시간.</summary>
    public double RestSeconds { get; init; } = 0.6;
}

public enum FsmBossState
{
    Approach,
    Attack,
    Rest,
}

/// <summary>FSM 보스: 접근 → 공격(고정 순환) → 휴식 → 접근. 오래 쫓으면 돌진.</summary>
public sealed class FsmBossBrain(FsmBossParameters? parameters = null) : IBrain, IExplainableBrain
{
    private readonly FsmBossParameters _parameters = parameters ?? new FsmBossParameters();
    private StateMachine<FsmBossState> _machine = null!;
    private int _rotationIndex;
    private ActionId _chosen;
    private float _distance;

    public string Name => "FSM Boss";

    public FsmBossParameters Parameters => _parameters;

    public void Reset(BrainContext context)
    {
        var p = _parameters;
        int chaseTicks = context.Rules.ToTicks(p.ChaseSecondsBeforeCharge);
        int restTicks = context.Rules.ToTicks(p.RestSeconds);
        _rotationIndex = 0;

        _machine = new StateMachine<FsmBossState>(FsmBossState.Approach)
            .State(FsmBossState.Approach, new DelegateState(o => _distance > p.CloseRange
                ? Intent.Move(TowardPlayer(o))
                : Intent.None))
            .State(FsmBossState.Attack, new DelegateState(
                o => _machine.TicksInState == 0 ? Intent.Use(_chosen, TowardPlayer(o)) : Intent.None,
                enter: _ => ChoosePattern()))
            .State(FsmBossState.Rest, new DelegateState(_ => Intent.None))
            .On(FsmBossState.Approach, FsmBossState.Attack, "사거리 안",
                o => !o.Self.IsBusy && _distance <= p.CloseRange)
            .On(FsmBossState.Approach, FsmBossState.Attack, "오래 쫓음 → 돌진",
                o => !o.Self.IsBusy && _machine.TicksInState >= chaseTicks
                     && _distance >= p.ChargeMinRange && _distance <= p.ChargeMaxRange)
            .On(FsmBossState.Attack, FsmBossState.Rest, "공격 끝",
                o => _machine.TicksInState > 0 && !o.Self.IsBusy)
            .On(FsmBossState.Rest, FsmBossState.Approach, "휴식 끝",
                _ => _machine.TicksInState >= restTicks);
    }

    public Intent Decide(in Observation observation)
    {
        _distance = Vector2.Distance(observation.Self.Position, observation.Opponent.Position);
        return _machine.Tick(observation);
    }

    public DecisionTrace Explain()
    {
        var rotation = _parameters.Rotation;
        var notes = new List<string>
        {
            $"거리 {_distance:F1}m (근접 {_parameters.CloseRange}m)",
            $"다음 근접 패턴: {ActionNames.Korean(rotation[_rotationIndex % rotation.Count])}",
        };
        if (_machine.Current == FsmBossState.Attack)
            notes.Insert(0, $"고른 패턴: {ActionNames.Korean(_chosen)}");

        return new DecisionTrace(StateName(_machine.Current), _machine.TicksInState, TransitionText(), notes);
    }

    private void ChoosePattern()
    {
        if (_distance > _parameters.CloseRange)
        {
            _chosen = ActionId.BossCharge;
            return;
        }

        var rotation = _parameters.Rotation;
        _chosen = rotation[_rotationIndex % rotation.Count];
        _rotationIndex++;
    }

    private static Vector2 TowardPlayer(in Observation o) =>
        VectorMath.NormalizeOr(o.Opponent.Position - o.Self.Position, o.Self.Facing);

    private string? TransitionText() => _machine.LastTransition is { } t
        ? $"{StateName(t.From!.Value)} → {StateName(t.To)}: {t.Reason} (틱 {_machine.LastTransitionTick})"
        : null;

    public static string StateName(FsmBossState state) => state switch
    {
        FsmBossState.Approach => "접근",
        FsmBossState.Attack => "공격",
        FsmBossState.Rest => "휴식",
        _ => state.ToString(),
    };
}
