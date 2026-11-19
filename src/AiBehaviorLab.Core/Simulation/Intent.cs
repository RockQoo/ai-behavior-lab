using System.Numerics;
using AiBehaviorLab.Rules;

namespace AiBehaviorLab.Simulation;

public enum IntentKind
{
    None,
    Move,
    Action,
}

/// <summary>
/// Brain이 한 틱에 내는 의도. 시뮬레이션이 규칙(행동 중인지, 스태미나, 회복약)을 검사해서 받아들이거나 무시한다.
/// Direction은 이동 방향 또는 행동의 조준 방향. 길이는 상관없고, 0이면 지금 바라보는 방향을 쓴다.
/// </summary>
public readonly record struct Intent(IntentKind Kind, Vector2 Direction, ActionId Action)
{
    public static Intent None => default;

    public static Intent Move(Vector2 direction) => new(IntentKind.Move, direction, default);

    public static Intent Use(ActionId action, Vector2 direction) => new(IntentKind.Action, direction, action);
}
