using AiBehaviorLab.Brains;
using AiBehaviorLab.Brains.Baseline;
using AiBehaviorLab.Brains.Fsm;
using AiBehaviorLab.Rules;

namespace AiBehaviorLab.Replay;

/// <summary>리플레이 머리말의 이름으로 규칙과 Brain을 다시 만든다. 새 Brain을 만들면 여기에 등록한다.</summary>
public static class ReplayCatalog
{
    public const string RulesV0 = "v0";
    public const string BaselinePlayer = "baseline-player";
    public const string BaselineBoss = "baseline-boss";
    public const string FsmPlayer = "fsm-player";
    public const string FsmBoss = "fsm-boss";
    public const string Idle = "idle";

    private static readonly Dictionary<string, Func<IBrain>> Brains = new()
    {
        [BaselinePlayer] = () => new BaselinePlayerBrain(),
        [BaselineBoss] = () => new BaselineBossBrain(),
        [FsmPlayer] = () => new FsmPlayerBrain(),
        [FsmBoss] = () => new FsmBossBrain(),
        [Idle] = () => new IdleBrain(),
    };

    /// <summary>러너·화면의 짧은 이름(baseline, fsm)을 등록 이름으로. 모르면 null.</summary>
    public static string? PlayerBrainByKind(string kind) => kind switch
    {
        "baseline" => BaselinePlayer,
        "fsm" => FsmPlayer,
        _ => null,
    };

    public static string? BossBrainByKind(string kind) => kind switch
    {
        "baseline" => BaselineBoss,
        "fsm" => FsmBoss,
        _ => null,
    };

    public static IReadOnlyCollection<string> BrainNames => Brains.Keys;

    public static CombatRules CreateRules(string name) => name switch
    {
        RulesV0 => DefaultRules.V0(),
        _ => throw new KeyNotFoundException($"모르는 규칙 이름: {name}"),
    };

    public static IBrain CreateBrain(string name)
    {
        return Brains.TryGetValue(name, out var factory)
            ? factory()
            : throw new KeyNotFoundException($"모르는 Brain 이름: {name}");
    }
}
