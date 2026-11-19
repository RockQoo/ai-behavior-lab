using AiBehaviorLab.Replay;
using AiBehaviorLab.Simulation;

/// <summary>러너의 replay(한 판 기록), verify(다시 돌려 해시 비교) 명령.</summary>
internal static class ReplayCommands
{
    public static int Record(string[] args)
    {
        ulong seed = 1;
        string limitsName = "casual";
        string? output = null;

        for (int i = 0; i + 1 < args.Length; i += 2)
        {
            switch (args[i])
            {
                case "--seed" when ulong.TryParse(args[i + 1], out ulong s):
                    seed = s;
                    break;
                case "--limits" when RunnerOptions.LimitsByName(args[i + 1]) is not null:
                    limitsName = args[i + 1];
                    break;
                case "--out":
                    output = args[i + 1];
                    break;
                default:
                    Console.Error.WriteLine($"알 수 없는 옵션이거나 값이 잘못됐다: {args[i]} {args[i + 1]}");
                    return 2;
            }
        }
        if (args.Length % 2 != 0)
        {
            Console.Error.WriteLine($"{args[^1]} 뒤에 값이 없다");
            return 2;
        }

        output ??= Path.Combine("replays", $"seed-{seed}-{limitsName}.json");
        var spec = new MatchSpec(ReplayCatalog.RulesV0, seed, ReplayCatalog.BaselinePlayer, ReplayCatalog.BaselineBoss,
            RunnerOptions.LimitsByName(limitsName)!, HumanLimits.None);

        var replay = ReplayRecorder.Record(spec);
        replay.Save(output);

        var header = replay.Header;
        Console.WriteLine($"리플레이 저장: {Path.GetFullPath(output)}");
        Console.WriteLine($"  시드 {seed}, 사람 한계 {limitsName}, {header.Winner} 승({header.Reason}), " +
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
