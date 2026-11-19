namespace AiBehaviorLab.Brains;

/// <summary>
/// 한 틱의 판단 근거. 관전 화면의 "판단 근거" 패널에 그대로 보인다.
/// FSM은 상태와 전이 이유, 유틸리티 AI는 후보 점수 같은 것을 Notes에 담는다.
/// </summary>
/// <param name="State">지금 상태(또는 고른 행동) 이름.</param>
/// <param name="TicksInState">그 상태에서 보낸 틱 수.</param>
/// <param name="LastTransition">마지막으로 바뀐 이유. 예: "접근 → 공격: 사거리 안 (틱 512)".</param>
/// <param name="Notes">판단에 쓴 값들. 예: "거리 2.3m", "스태미나 54".</param>
public sealed record DecisionTrace(string State, int TicksInState, string? LastTransition, IReadOnlyList<string> Notes);

/// <summary>판단 근거를 낼 수 있는 Brain. Decide 바로 뒤에 불린다(기록할 때만).</summary>
public interface IExplainableBrain
{
    DecisionTrace Explain();
}
