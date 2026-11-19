using System.Numerics;
using AiBehaviorLab.Rules;

namespace AiBehaviorLab.Simulation;

/// <summary>진행 중인 행동. 단계 → 국면(선딜/판정/후딜) → 경과 틱.</summary>
internal sealed class RunningAction
{
    public RunningAction(ActionDefinition definition)
    {
        Definition = definition;
    }

    public ActionDefinition Definition { get; }
    public int StepIndex { get; set; }
    public ActionPhase Phase { get; set; }
    public int PhaseElapsed { get; set; }
    public Vector2 AimOrigin { get; set; }
    public Vector2 AimDirection { get; set; }

    /// <summary>이번 단계의 판정이 이미 끝났는지(맞혔거나 무적에 막혔다).</summary>
    public bool StepResolved { get; set; }

    public ActionStep Step => Definition.Steps[StepIndex];

    public int PhaseLength => Phase switch
    {
        ActionPhase.Windup => Step.WindupTicks,
        ActionPhase.Active => Step.ActiveTicks,
        _ => Definition.RecoveryTicks,
    };

    public ActionView ToView() =>
        new(Definition.Id, StepIndex, Phase, PhaseElapsed, PhaseLength, AimOrigin, AimDirection);
}

/// <summary>시뮬레이션 안의 전투원(변경 가능). 밖으로는 CombatantView로만 보여 준다.</summary>
internal sealed class Combatant
{
    public Combatant(Side side, CombatantStats stats, Vector2 position, Vector2 facing)
    {
        Side = side;
        Stats = stats;
        Position = position;
        Facing = facing;
        Hp = stats.MaxHp;
        Stamina = stats.MaxStamina;
        Potions = stats.Potions;
    }

    public Side Side { get; }
    public CombatantStats Stats { get; }
    public Vector2 Position { get; set; }
    public Vector2 Facing { get; set; }
    public int Hp { get; set; }
    public float Stamina { get; set; }
    public int Potions { get; set; }
    public RunningAction? Action { get; set; }
    public CombatStats Statistics { get; } = new();

    // 한 틱 동안만 쓰는 값(Advance에서 정하고 판정·충돌에서 읽는다)
    public bool IsInvulnerable { get; set; }
    public bool IsDashing { get; set; }
    public ActionStep? StrikeStep { get; set; }

    public bool IsAlive => Hp > 0;

    public CombatantView ToView() => new(
        Side, Position, Facing, Stats.BodyRadius, Hp, Stats.MaxHp, Stamina, Potions,
        IsInvulnerable, Action?.ToView());
}
