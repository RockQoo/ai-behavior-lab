using AiBehaviorLab.Brains;
using AiBehaviorLab.Rules;

namespace AiBehaviorLab.Simulation;

/// <summary>한 판의 설정.</summary>
public sealed record MatchSetup(ulong Seed, IBrain PlayerBrain, IBrain BossBrain)
{
    public HumanLimits PlayerLimits { get; init; } = HumanLimits.None;
    public HumanLimits BossLimits { get; init; } = HumanLimits.None;

    /// <summary>사건 기록을 남길지. 대량 실행에서는 끈다.</summary>
    public bool RecordEvents { get; init; }

    /// <summary>Brain의 판단 근거(IExplainableBrain)를 매 틱 받아 둘지. 리플레이 기록할 때만 켠다.</summary>
    public bool RecordDecisions { get; init; }
}

public enum EndReason
{
    PlayerDefeated,
    BossDefeated,
    Timeout,
}

public enum CombatEventKind
{
    ActionStarted,
    Hit,

    /// <summary>판정이 겹쳤지만 무적이라 피했다.</summary>
    Dodged,

    Healed,
    MatchEnded,
}

/// <summary>사건 기록 한 줄. Source는 행동한 쪽.</summary>
public readonly record struct CombatEvent(int Tick, CombatEventKind Kind, Side Source, ActionId? Action, int Value);

/// <summary>한 판 동안 한 전투원의 집계.</summary>
public sealed class CombatStats
{
    public int ActionsStarted { get; internal set; }

    /// <summary>규칙에 맞지 않아 무시된 행동 의도(스태미나 부족, 회복약 없음, 남의 행동).</summary>
    public int RejectedIntents { get; internal set; }

    public int Hits { get; internal set; }
    public int DamageDealt { get; internal set; }

    /// <summary>상대 공격을 무적으로 피한 횟수.</summary>
    public int Dodges { get; internal set; }

    public int Mistakes { get; internal set; }
    public int HealAmount { get; internal set; }
}

public sealed record MatchResult(
    Side Winner,
    EndReason Reason,
    int Ticks,
    double Seconds,
    int PlayerHp,
    int BossHp,
    CombatStats Player,
    CombatStats Boss,
    IReadOnlyList<CombatEvent> Events);
