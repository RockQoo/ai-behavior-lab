using AiBehaviorLab.Math;
using AiBehaviorLab.Rules;
using AiBehaviorLab.Simulation;

namespace AiBehaviorLab.Brains;

/// <summary>판이 시작될 때 Brain이 받는 정보. Random은 이 Brain만 쓰는 난수 흐름이다.</summary>
public sealed record BrainContext(Side Side, CombatRules Rules, DeterministicRandom Random);

/// <summary>
/// 의사결정의 자리. FSM, BT, GOAP, HTN, 유틸리티 AI, 또는 그 조합이 이것을 구현한다.
/// 관찰만 보고 의도만 낸다. 상태를 가질 수 있으므로 판마다 새 인스턴스를 쓰거나 Reset에서 초기화한다.
/// </summary>
public interface IBrain
{
    string Name { get; }

    /// <summary>판이 시작될 때 한 번 부른다.</summary>
    void Reset(BrainContext context);

    /// <summary>매 틱 부른다. 행동 중이면 의도는 무시되지만 그래도 불린다(관찰을 쌓을 수 있게).</summary>
    Intent Decide(in Observation observation);
}
