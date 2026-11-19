using AiBehaviorLab.Brains;
using AiBehaviorLab.Brains.Baseline;
using AiBehaviorLab.Rules;

namespace AiBehaviorLab.Replay;

/// <summary>리플레이 머리말의 이름으로 규칙과 Brain을 다시 만든다. 새 Brain을 만들면 여기에 등록한다.</summary>
public static class ReplayCatalog
{
    public const string RulesV0 = "v0";
    public const string BaselinePlayer = "baseline-player";
    public const string BaselineBoss = "baseline-boss";
    public const string Idle = "idle";

    private static readonly Dictionary<string, Func<IBrain>> Brains = new()
    {
        [BaselinePlayer] = () => new BaselinePlayerBrain(),
        [BaselineBoss] = () => new BaselineBossBrain(),
        [Idle] = () => new IdleBrain(),
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
