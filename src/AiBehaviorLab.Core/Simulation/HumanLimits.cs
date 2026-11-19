namespace AiBehaviorLab.Simulation;

/// <summary>
/// 사람 수준의 한계. 봇이 1틱 만에 반응해서 모든 공격을 피하지 못하게 한다.
/// </summary>
/// <param name="ReactionSeconds">상대 상태를 이만큼 늦게 본다.</param>
/// <param name="PositionError">상대 위치와 조준 기준점을 이 반지름 안에서 흔들어 본다(m).</param>
/// <param name="MistakeChance">행동을 시작할 때 방향이 ±90° 안에서 틀어질 확률.</param>
public sealed record HumanLimits(double ReactionSeconds, float PositionError, double MistakeChance)
{
    public static HumanLimits None { get; } = new(0, 0f, 0);
    public static HumanLimits Casual { get; } = new(0.30, 0.4f, 0.05);
    public static HumanLimits Expert { get; } = new(0.18, 0.15f, 0.01);
}
