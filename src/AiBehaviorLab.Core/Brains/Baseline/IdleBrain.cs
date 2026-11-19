using AiBehaviorLab.Simulation;

namespace AiBehaviorLab.Brains.Baseline;

/// <summary>아무것도 하지 않는다. 테스트와 기준선용.</summary>
public sealed class IdleBrain : IBrain
{
    public string Name => "Idle";

    public void Reset(BrainContext context)
    {
    }

    public Intent Decide(in Observation observation) => Intent.None;
}
