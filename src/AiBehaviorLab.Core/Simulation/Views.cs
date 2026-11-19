using System.Numerics;
using AiBehaviorLab.Rules;

namespace AiBehaviorLab.Simulation;

/// <summary>진행 중인 행동의 읽기 전용 모습. 예고 범위는 Telegraph로 판정한다.</summary>
public readonly record struct ActionView(
    ActionId Id,
    int StepIndex,
    ActionPhase Phase,
    int PhaseElapsed,
    int PhaseLength,
    Vector2 AimOrigin,
    Vector2 AimDirection)
{
    public int PhaseRemaining => PhaseLength - PhaseElapsed;
}

/// <summary>한 전투원의 읽기 전용 모습. Brain과 관전 화면이 본다.</summary>
public readonly record struct CombatantView(
    Side Side,
    Vector2 Position,
    Vector2 Facing,
    float BodyRadius,
    int Hp,
    int MaxHp,
    float Stamina,
    int Potions,
    bool IsInvulnerable,
    ActionView? Action)
{
    public bool IsBusy => Action is not null;
}

/// <summary>
/// Brain이 한 틱에 받는 관찰. 자기 상태는 정확하고, 상대 상태는 사람 한계(반응 지연, 위치 오차)가 적용된 값이다.
/// </summary>
public readonly record struct Observation(int Tick, CombatRules Rules, CombatantView Self, CombatantView Opponent);
