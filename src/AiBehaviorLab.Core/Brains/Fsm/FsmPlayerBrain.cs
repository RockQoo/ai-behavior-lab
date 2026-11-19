using System.Numerics;
using AiBehaviorLab.Math;
using AiBehaviorLab.Rules;
using AiBehaviorLab.Simulation;

namespace AiBehaviorLab.Brains.Fsm;

/// <summary>FSM 플레이어의 문턱값. 3단계에서 진화시킬 후보다.</summary>
public sealed record FsmPlayerParameters
{
    /// <summary>보이는 예고 남은 틱이 이하가 되면 구른다(반응 지연만큼 늦게 보이는 것을 감안한 값).</summary>
    public int RollLeadTicks { get; init; } = 12;

    public float PotionHpRatio { get; init; } = 0.4f;
    public float SafePotionDistance { get; init; } = 6f;

    /// <summary>스태미나가 이보다 낮으면 물러나서 숨을 고른다(구르기 30 + 여유).</summary>
    public float LowStamina { get; init; } = 35f;

    /// <summary>숨 고르기를 끝내는 스태미나.</summary>
    public float ResumeStamina { get; init; } = 70f;

    /// <summary>숨 고르기 중 보스와 벌려 둘 거리.</summary>
    public float RegainDistance { get; init; } = 6.5f;

    /// <summary>평타 사거리에서 이만큼 안쪽까지 다가간다.</summary>
    public float RangeMargin { get; init; } = 0.2f;
}

public enum FsmPlayerState
{
    Approach,
    Hold,
    Punish,
    Evade,
    Heal,
    Regain,
}

/// <summary>
/// FSM 플레이어. 인터럽트(어느 상태에서든): 예고 범위 안 → 회피, HP 낮고 안전 → 회복, 스태미나 부족 → 숨 고르기.
/// 평소: 접근 → 대기(사거리) → 보스 후딜이면 반격 → 대기.
/// </summary>
public sealed class FsmPlayerBrain(FsmPlayerParameters? parameters = null) : IBrain, IExplainableBrain
{
    private readonly FsmPlayerParameters _parameters = parameters ?? new FsmPlayerParameters();
    private StateMachine<FsmPlayerState> _machine = null!;
    private Situation _now;

    public string Name => "FSM Player";

    public FsmPlayerParameters Parameters => _parameters;

    /// <summary>한 틱 동안 여러 조건이 같이 쓰는 값. Decide 처음에 한 번 계산한다.</summary>
    private struct Situation
    {
        public float Distance;
        public float Reach;
        public Vector2 TowardBoss;
        public ActionView? Threat;
        public bool Threatened;
        public bool StrikeSoon;
        public bool BossRecovering;
        public int BossRecoveryLeft;
        public bool LowHp;
    }

    public void Reset(BrainContext context)
    {
        var p = _parameters;
        var rules = context.Rules;
        float rollCost = rules.Get(ActionId.Roll).StaminaCost;
        var light = rules.Get(ActionId.LightAttack);
        int potionTicks = rules.Get(ActionId.Potion).Steps[0].WindupTicks;

        bool Free(in Observation o) => !o.Self.IsBusy;
        bool SafeToHeal() => _now.Distance > p.SafePotionDistance || _now.BossRecoveryLeft >= potionTicks;

        _machine = new StateMachine<FsmPlayerState>(FsmPlayerState.Approach)
            .State(FsmPlayerState.Approach, new DelegateState(_ => Intent.Move(_now.TowardBoss)))
            .State(FsmPlayerState.Hold, new DelegateState(_ => Intent.None))
            .State(FsmPlayerState.Punish, new DelegateState(_ =>
                _machine.TicksInState == 0 ? Intent.Use(ActionId.LightAttack, _now.TowardBoss) : Intent.None))
            .State(FsmPlayerState.Evade, new DelegateState(o => Evade(o, rollCost)))
            .State(FsmPlayerState.Heal, new DelegateState(_ =>
                _machine.TicksInState == 0 ? Intent.Use(ActionId.Potion, _now.TowardBoss) : Intent.None))
            .State(FsmPlayerState.Regain, new DelegateState(_ =>
                _now.Distance < p.RegainDistance ? Intent.Move(-_now.TowardBoss) : Intent.None))

            // 인터럽트: 위에 있을수록 먼저
            .FromAny(FsmPlayerState.Evade, "예고 범위 안", o => Free(o) && _now.Threatened)
            .FromAny(FsmPlayerState.Heal, "HP 낮음 · 안전",
                o => Free(o) && !_now.Threatened && _now.LowHp && o.Self.Potions > 0 && SafeToHeal())
            .FromAny(FsmPlayerState.Regain, "스태미나 부족",
                o => Free(o) && !_now.Threatened && o.Self.Stamina < p.LowStamina)

            // 평소 흐름
            .On(FsmPlayerState.Evade, FsmPlayerState.Approach, "벗어남", o => Free(o) && !_now.Threatened)
            .On(FsmPlayerState.Heal, FsmPlayerState.Approach, "다 마심", o => _machine.TicksInState > 0 && Free(o))
            .On(FsmPlayerState.Regain, FsmPlayerState.Approach, "스태미나 회복", o => o.Self.Stamina >= p.ResumeStamina)
            .On(FsmPlayerState.Approach, FsmPlayerState.Hold, "사거리 도착", _ => _now.Distance <= _now.Reach)
            .On(FsmPlayerState.Hold, FsmPlayerState.Approach, "멀어짐", _ => _now.Distance > _now.Reach + 0.5f)
            .On(FsmPlayerState.Hold, FsmPlayerState.Approach, "보스 후딜 · 사거리 밖",
                _ => _now.BossRecovering && _now.Distance > _now.Reach)
            .On(FsmPlayerState.Hold, FsmPlayerState.Punish, "보스 후딜",
                o => Free(o) && _now.BossRecovering && _now.Distance <= _now.Reach && o.Self.Stamina >= light.StaminaCost)
            .On(FsmPlayerState.Punish, FsmPlayerState.Hold, "공격 끝", o => _machine.TicksInState > 0 && Free(o));
    }

    public Intent Decide(in Observation observation)
    {
        _now = Read(observation);
        return _machine.Tick(observation);
    }

    public DecisionTrace Explain()
    {
        var notes = new List<string>
        {
            $"거리 {_now.Distance:F1}m (사거리 {_now.Reach:F1}m)",
            _now.Threat is { } threat
                ? $"보스 {ActionNames.Korean(threat.Id)} {PhaseName(threat.Phase)} {threat.PhaseRemaining}틱"
                  + (_now.Threatened ? " · 범위 안" : " · 범위 밖")
                : "보스 대기",
        };
        if (_machine.Current == FsmPlayerState.Evade)
            notes.Add(_now.StrikeSoon ? $"판정 임박(≤{_parameters.RollLeadTicks}틱) → 구르기" : "아직 이르다 → 걸어서 벗어남");
        if (_now.LowHp)
            notes.Add($"HP 낮음(≤{_parameters.PotionHpRatio:P0})");

        return new DecisionTrace(StateName(_machine.Current), _machine.TicksInState, TransitionText(), notes);
    }

    private Situation Read(in Observation o)
    {
        var self = o.Self;
        var boss = o.Opponent;
        var toBoss = boss.Position - self.Position;
        var light = (SectorShape)o.Rules.Get(ActionId.LightAttack).Steps[0].Shape!;

        var situation = new Situation
        {
            Distance = toBoss.Length(),
            Reach = light.Radius + boss.BodyRadius - _parameters.RangeMargin,
            TowardBoss = VectorMath.NormalizeOr(toBoss, self.Facing),
            Threat = boss.Action,
            LowHp = self.Hp <= self.MaxHp * _parameters.PotionHpRatio,
        };

        if (boss.Action is { } action)
        {
            situation.Threatened = Telegraph.Threatens(o.Rules, action, self.Position, self.BodyRadius);
            situation.StrikeSoon = action.Phase == ActionPhase.Active || action.PhaseRemaining <= _parameters.RollLeadTicks;
            situation.BossRecovering = action.Phase == ActionPhase.Recovery;
            situation.BossRecoveryLeft = action.Phase == ActionPhase.Recovery ? action.PhaseRemaining : 0;
        }

        return situation;
    }

    private Intent Evade(in Observation o, float rollCost)
    {
        if (_now.Threat is not { } threat)
            return Intent.None;

        var away = VectorMath.NormalizeOr(o.Self.Position - threat.AimOrigin, -_now.TowardBoss);
        return _now.StrikeSoon && o.Self.Stamina >= rollCost
            ? Intent.Use(ActionId.Roll, away)
            : Intent.Move(away);
    }

    private string? TransitionText() => _machine.LastTransition is { } t
        ? $"{(t.From is { } from ? StateName(from) : "어디서든")} → {StateName(t.To)}: {t.Reason} (틱 {_machine.LastTransitionTick})"
        : null;

    private static string PhaseName(ActionPhase phase) => phase switch
    {
        ActionPhase.Windup => "예고",
        ActionPhase.Active => "판정",
        _ => "후딜",
    };

    public static string StateName(FsmPlayerState state) => state switch
    {
        FsmPlayerState.Approach => "접근",
        FsmPlayerState.Hold => "대기",
        FsmPlayerState.Punish => "반격",
        FsmPlayerState.Evade => "회피",
        FsmPlayerState.Heal => "회복",
        FsmPlayerState.Regain => "숨 고르기",
        _ => state.ToString(),
    };
}
