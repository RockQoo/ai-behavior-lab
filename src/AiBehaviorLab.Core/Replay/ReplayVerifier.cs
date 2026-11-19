namespace AiBehaviorLab.Replay;

/// <param name="Matches">모든 프레임의 해시가 같다.</param>
/// <param name="FramesChecked">비교한 프레임 수.</param>
/// <param name="FirstMismatchTick">처음 갈라진 틱. 같거나 파일 구조가 잘못됐으면 null.</param>
/// <param name="Problem">파일 구조가 잘못됐을 때 그 이유.</param>
public sealed record ReplayVerification(bool Matches, int FramesChecked, int? FirstMismatchTick, string? Problem = null);

/// <summary>
/// 리플레이 머리말의 스펙으로 다시 시뮬레이션해서 프레임마다 상태 해시를 비교한다.
/// 다른 런타임(예: Godot 안의 .NET)에서 돌려 보면 결정성이 어디서 깨지는지 알 수 있다.
/// </summary>
public static class ReplayVerifier
{
    public static ReplayVerification Verify(ReplayFile replay)
    {
        // 프레임 i = 틱 i가 보장돼야 한 틱씩 비교할 수 있다(빠진 프레임을 건너뛰고 통과하지 않게).
        try
        {
            replay.Validate();
        }
        catch (InvalidDataException e)
        {
            return new ReplayVerification(false, 0, null, e.Message);
        }

        var spec = replay.Header.Spec;
        var simulation = ReplayRecorder.CreateSimulation(spec, ReplayCatalog.CreateRules(spec.RulesName));

        for (int i = 0; i < replay.Frames.Count; i++)
        {
            if (i > 0)
            {
                if (simulation.IsOver)
                    return new ReplayVerification(false, i, i); // 기록이 판보다 길다
                simulation.Step();
            }

            string hash = ReplayRecorder.FormatHash(simulation.ComputeStateHash());
            if (hash != replay.Frames[i].Hash)
                return new ReplayVerification(false, i, i);
        }

        // 기록보다 판이 길게 이어지면 그것도 갈라진 것이다.
        if (!simulation.IsOver)
            return new ReplayVerification(false, replay.Frames.Count, simulation.Tick);

        return new ReplayVerification(true, replay.Frames.Count, null);
    }
}
