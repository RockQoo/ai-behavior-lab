namespace AiBehaviorLab.Replay;

/// <param name="Matches">모든 프레임의 해시가 같다.</param>
/// <param name="FramesChecked">비교한 프레임 수.</param>
/// <param name="FirstMismatchTick">처음 갈라진 틱. 같으면 null.</param>
public sealed record ReplayVerification(bool Matches, int FramesChecked, int? FirstMismatchTick);

/// <summary>
/// 리플레이 머리말의 스펙으로 다시 시뮬레이션해서 프레임마다 상태 해시를 비교한다.
/// 다른 런타임(예: Godot 안의 .NET)에서 돌려 보면 결정성이 어디서 깨지는지 알 수 있다.
/// </summary>
public static class ReplayVerifier
{
    public static ReplayVerification Verify(ReplayFile replay)
    {
        var spec = replay.Header.Spec;
        var simulation = ReplayRecorder.CreateSimulation(spec, ReplayCatalog.CreateRules(spec.RulesName));

        int checkedCount = 0;
        foreach (var frame in replay.Frames)
        {
            while (simulation.Tick < frame.Tick && !simulation.IsOver)
                simulation.Step();

            string hash = ReplayRecorder.FormatHash(simulation.ComputeStateHash());
            if (simulation.Tick != frame.Tick || hash != frame.Hash)
                return new ReplayVerification(false, checkedCount, frame.Tick);
            checkedCount++;
        }

        // 기록보다 판이 길게 이어지면 그것도 갈라진 것이다.
        if (!simulation.IsOver)
            return new ReplayVerification(false, checkedCount, simulation.Tick);

        return new ReplayVerification(true, checkedCount, null);
    }
}
