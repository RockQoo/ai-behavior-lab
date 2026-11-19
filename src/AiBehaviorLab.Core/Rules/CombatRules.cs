using System.Collections.Frozen;
using System.Numerics;

namespace AiBehaviorLab.Rules;

/// <summary>전투 규칙 전체. 숫자는 docs/combat-rules.md와 맞춘다.</summary>
public sealed record CombatRules
{
    public required int TickRate { get; init; }
    public required float ArenaRadius { get; init; }
    public required int TimeLimitTicks { get; init; }
    public required Vector2 PlayerStart { get; init; }
    public required Vector2 BossStart { get; init; }
    public required CombatantStats Player { get; init; }
    public required CombatantStats Boss { get; init; }
    private readonly FrozenDictionary<ActionId, ActionDefinition> _actions = FrozenDictionary<ActionId, ActionDefinition>.Empty;

    /// <summary>받은 사전을 불변 사본으로 바꿔 둔다. 여러 판과 Brain이 같은 규칙을 공유해도 안전하다.</summary>
    public required IReadOnlyDictionary<ActionId, ActionDefinition> Actions
    {
        get => _actions;
        init => _actions = value.ToFrozenDictionary();
    }

    public CombatantStats StatsOf(Side side) => side == Side.Player ? Player : Boss;

    public ActionDefinition Get(ActionId id)
    {
        return Actions.TryGetValue(id, out var definition)
            ? definition
            : throw new KeyNotFoundException($"규칙에 없는 행동: {id}");
    }

    public int ToTicks(double seconds) => ToTicks(seconds, TickRate);

    /// <summary>초를 틱으로. 0.5는 올림(0.25초 × 30 = 7.5 → 8틱).</summary>
    public static int ToTicks(double seconds, int tickRate)
    {
        return (int)System.Math.Round(seconds * tickRate, MidpointRounding.AwayFromZero);
    }

    public double ToSeconds(int ticks) => (double)ticks / TickRate;

    /// <summary>규칙이 시뮬레이션 전제를 지키는지 확인한다. 어기면 예외.</summary>
    public void Validate()
    {
        if (TickRate <= 0) throw new InvalidOperationException("TickRate는 1 이상");
        if (TimeLimitTicks <= 0) throw new InvalidOperationException("TimeLimitTicks는 1 이상");
        foreach (var (id, definition) in Actions)
        {
            if (definition.Id != id)
                throw new InvalidOperationException($"{id}: 키와 정의 Id가 다르다");
            if (definition.Steps.Count == 0)
                throw new InvalidOperationException($"{id}: 단계가 없다");
            if (definition.RecoveryTicks < 0)
                throw new InvalidOperationException($"{id}: 후딜이 음수");
            foreach (var step in definition.Steps)
            {
                if (step.ActiveTicks < 1)
                    throw new InvalidOperationException($"{id}: 판정 단계는 1틱 이상");
                if (step.WindupTicks < 0)
                    throw new InvalidOperationException($"{id}: 선딜이 음수");
                if (step.Damage > 0 && step.Shape is null)
                    throw new InvalidOperationException($"{id}: 피해가 있는데 범위가 없다");
                if (step.IsMoving && step.Damage > 0)
                {
                    // 예고 = 판정: 직선은 이동 거리 + 폭/2(끝에서 몸이 닿는 곳)까지 덮어야 한다.
                    if (step.Shape is not LineShape line)
                        throw new InvalidOperationException($"{id}: 이동형 공격 범위는 LineShape");
                    if (line.Length < step.MoveDistance + line.Width * 0.5f - 1e-4f)
                        throw new InvalidOperationException($"{id}: 직선 길이가 이동 거리 + 폭/2보다 짧다");
                }
            }
        }
    }
}
