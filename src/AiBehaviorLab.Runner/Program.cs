using System.Diagnostics;
using System.Text;
using AiBehaviorLab.Brains.Baseline;
using AiBehaviorLab.Rules;
using AiBehaviorLab.Simulation;

Console.OutputEncoding = Encoding.UTF8;

if (!RunnerOptions.TryParse(args, out var options, out string? error))
{
    Console.Error.WriteLine(error);
    Console.Error.WriteLine("사용법: --matches <판 수> --seed <시드> --limits none|casual|expert");
    return 2;
}

var rules = DefaultRules.V0();

MatchResult RunOne(ulong seed, bool recordEvents)
{
    var setup = new MatchSetup(seed, new BaselinePlayerBrain(), new BaselineBossBrain())
    {
        PlayerLimits = options.Limits,
        RecordEvents = recordEvents,
    };
    return new CombatSimulation(rules, setup).Run();
}

// 결정성 확인: 같은 시드를 두 번 돌려 사건 기록까지 같아야 한다.
var first = RunOne(options.Seed, recordEvents: true);
var second = RunOne(options.Seed, recordEvents: true);
bool deterministic = first.Events.SequenceEqual(second.Events)
                     && first.Ticks == second.Ticks
                     && first.PlayerHp == second.PlayerHp
                     && first.BossHp == second.BossHp;

var results = new MatchResult[options.Matches];
var stopwatch = Stopwatch.StartNew();
Parallel.For(0, options.Matches, i => results[i] = RunOne(options.Seed + (ulong)i, recordEvents: true));
stopwatch.Stop();

long totalTicks = results.Sum(r => (long)r.Ticks);
int playerWins = results.Count(r => r.Winner == Side.Player);
double seconds = stopwatch.Elapsed.TotalSeconds;

Console.WriteLine("ai-behavior-lab 러너 (기준선 플레이어 vs 기준선 보스)");
Console.WriteLine($"판 수 {options.Matches}, 시드 {options.Seed}~, 사람 한계 {options.LimitsName}");
Console.WriteLine();
Console.WriteLine($"플레이어 승률      {Percent(playerWins, options.Matches)}");
foreach (var reason in Enum.GetValues<EndReason>())
    Console.WriteLine($"  {ReasonName(reason),-14} {Percent(results.Count(r => r.Reason == reason), options.Matches)}");
Console.WriteLine($"평균 전투 시간     {results.Average(r => r.Seconds):F1}초");
Console.WriteLine($"평균 보스 남은 HP  {results.Average(r => r.BossHp):F0} / {rules.Boss.MaxHp}");
Console.WriteLine($"평균 플레이어 회피 {results.Average(r => r.Player.Dodges):F1}회, 피격 {results.Average(r => r.Boss.Hits):F1}회, 회복 {results.Average(r => r.Player.HealAmount):F0}");
Console.WriteLine($"평균 조작 실수     {results.Average(r => r.Player.Mistakes):F1}회");
Console.WriteLine();
Console.WriteLine("보스 패턴별 (판당 평균)   사용   명중   회피");
var bossEvents = results.SelectMany(r => r.Events).Where(e => e.Source == Side.Boss && e.Action is not null).ToList();
foreach (var group in bossEvents.GroupBy(e => e.Action!.Value).OrderBy(g => g.Key))
{
    double Per(CombatEventKind kind) => (double)group.Count(e => e.Kind == kind) / options.Matches;
    Console.WriteLine($"  {group.Key,-20} {Per(CombatEventKind.ActionStarted),5:F1}  {Per(CombatEventKind.Hit),5:F1}  {Per(CombatEventKind.Dodged),5:F1}");
}
Console.WriteLine();
Console.WriteLine($"처리 시간 {seconds:F2}초, {options.Matches / seconds:F0}판/초, {totalTicks / seconds / 1_000_000:F2}M틱/초");
Console.WriteLine($"결정성 확인(시드 {options.Seed} 두 번): {(deterministic ? "같음" : "다름!")}");

return deterministic ? 0 : 1;

static string Percent(int count, int total) => $"{100.0 * count / total,5:F1}% ({count})";

static string ReasonName(EndReason reason) => reason switch
{
    EndReason.BossDefeated => "보스 쓰러짐",
    EndReason.PlayerDefeated => "플레이어 쓰러짐",
    EndReason.Timeout => "시간 초과",
    _ => reason.ToString(),
};

internal sealed record RunnerOptions(int Matches, ulong Seed, HumanLimits Limits, string LimitsName)
{
    public static bool TryParse(string[] args, out RunnerOptions options, out string? error)
    {
        options = new RunnerOptions(1000, 1, HumanLimits.Casual, "casual");
        error = null;

        for (int i = 0; i < args.Length; i++)
        {
            string name = args[i];
            if (i + 1 >= args.Length)
            {
                error = $"{name} 뒤에 값이 없다";
                return false;
            }

            string value = args[++i];
            switch (name)
            {
                case "--matches" when int.TryParse(value, out int matches) && matches > 0:
                    options = options with { Matches = matches };
                    break;
                case "--seed" when ulong.TryParse(value, out ulong seed):
                    options = options with { Seed = seed };
                    break;
                case "--limits" when LimitsByName(value) is { } limits:
                    options = options with { Limits = limits, LimitsName = value };
                    break;
                default:
                    error = $"알 수 없는 옵션이거나 값이 잘못됐다: {name} {value}";
                    return false;
            }
        }

        return true;
    }

    private static HumanLimits? LimitsByName(string name) => name switch
    {
        "none" => HumanLimits.None,
        "casual" => HumanLimits.Casual,
        "expert" => HumanLimits.Expert,
        _ => null,
    };
}
