namespace AiBehaviorLab.Rules;

public enum Side
{
    Player,
    Boss,
}

public enum ActionId
{
    // 플레이어
    LightAttack,
    HeavyAttack,
    Roll,
    Potion,

    // 보스 기본 패턴
    BossSlam,
    BossSweep,
    BossCharge,
    BossShockwave,

    // 보스 변형 패턴(적응형 보스용)
    BossSlamDelayed,
    BossSweepDouble,
    BossChargeShort,
    BossShockwaveRing,
}

public enum ActionPhase
{
    /// <summary>선딜. 예고(노란색)가 보인다.</summary>
    Windup,

    /// <summary>판정. 공격 판정, 이동, 회복, 무적이 여기서 일어난다.</summary>
    Active,

    /// <summary>후딜. 상대의 반격 기회.</summary>
    Recovery,
}

/// <summary>단계의 선딜이 시작될 때 조준을 어떻게 정할지.</summary>
public enum AimMode
{
    /// <summary>첫 단계: 의도에 담긴 방향. 이후 단계: 이전 방향 유지, 기준점은 지금 위치.</summary>
    KeepPrevious,

    /// <summary>지금 위치에서 상대 쪽으로 다시 조준한다.</summary>
    TowardOpponent,
}

/// <summary>
/// 행동의 한 단계: 선딜 → 판정. 여러 단계를 이어 2연타 같은 패턴을 만든다.
/// 모든 단계가 끝나면 행동 전체의 후딜이 온다.
/// </summary>
public sealed record ActionStep(int WindupTicks, int ActiveTicks)
{
    /// <summary>예고와 판정 범위. null이면 공격하지 않는 단계(구르기, 회복약).</summary>
    public AttackShape? Shape { get; init; }

    public int Damage { get; init; }

    /// <summary>판정 단계 동안 조준 방향으로 이동하는 거리(구르기, 돌진). 0보다 크면 이동형 판정.</summary>
    public float MoveDistance { get; init; }

    /// <summary>판정 단계 시작부터 무적인 틱 수.</summary>
    public int InvulnerableTicks { get; init; }

    /// <summary>판정 단계 첫 틱에 회복하는 HP.</summary>
    public int Heal { get; init; }

    public AimMode Aim { get; init; } = AimMode.KeepPrevious;

    public bool IsMoving => MoveDistance > 0f;
}

public sealed record ActionDefinition(ActionId Id, Side User, IReadOnlyList<ActionStep> Steps, int RecoveryTicks)
{
    public float StaminaCost { get; init; }

    /// <summary>회복약을 하나 쓴다(시작할 때 차감).</summary>
    public bool UsesPotion { get; init; }

    /// <summary>변형 패턴이면 원래 패턴. 기본 패턴이면 null.</summary>
    public ActionId? VariantOf { get; init; }

    public int TotalTicks => Steps.Sum(s => s.WindupTicks + s.ActiveTicks) + RecoveryTicks;
}

public sealed record CombatantStats(
    int MaxHp,
    float MoveSpeed,
    float BodyRadius,
    float MaxStamina,
    float StaminaRegenPerSecond,
    int Potions);
