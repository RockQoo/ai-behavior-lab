using AiBehaviorLab.Replay;
using AiBehaviorLab.Simulation;

/// <summary>러너의 replay(한 판 기록), verify(다시 돌려 해시 비교) 명령.</summary>
internal static class ReplayCommands
{
    public static int Record(string[] args)
    {
        if (!RunnerOptions.TryParse(args, out var options, out string? error))
        {
            Console.Error.WriteLine(error);
            return 2;
        }

        ulong seed = options.Seed;
        string output = options.Output
            ?? Path.Combine("replays", $"seed-{seed}-{options.LimitsName}-{options.PlayerBrain}-vs-{options.BossBrain}.json");
        var spec = new MatchSpec(ReplayCatalog.RulesV0, seed, options.PlayerBrain, options.BossBrain,
            options.Limits, HumanLimits.None);

        var replay = ReplayRecorder.Record(spec);
        replay.Save(output);

        var header = replay.Header;
        Console.WriteLine($"리플레이 저장: {Path.GetFullPath(output)}");
        Console.WriteLine($"  시드 {seed}, 사람 한계 {options.LimitsName}, {header.Winner} 승({header.Reason}), " +
                          $"{header.Ticks}틱 = {(double)header.Ticks / header.TickRate:F1}초, 프레임 {replay.Frames.Count}");
        return 0;
    }

    public static int Verify(string[] args)
    {
        if (args.Length != 1)
        {
            Console.Error.WriteLine("사용법: verify <리플레이 파일>");
            return 2;
        }

        ReplayFile replay;
        try
        {
            replay = ReplayFile.Load(args[0]);
        }
        catch (Exception e) when (e is InvalidDataException or System.Text.Json.JsonException or IOException)
        {
            Console.WriteLine($"잘못된 리플레이: {e.Message}");
            return 1;
        }

        var result = ReplayVerifier.Verify(replay);
        Console.WriteLine(result switch
        {
            { Matches: true } => $"일치: 프레임 {result.FramesChecked}개 해시가 모두 같다",
            { Problem: { } problem } => $"잘못된 리플레이: {problem}",
            _ => $"갈라짐: 틱 {result.FirstMismatchTick}에서 해시가 다르다(앞의 프레임 {result.FramesChecked}개는 같음)",
        });
        return result.Matches ? 0 : 1;
    }
}
