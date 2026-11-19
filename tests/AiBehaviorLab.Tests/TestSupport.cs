using System.Numerics;
using AiBehaviorLab.Brains;
using AiBehaviorLab.Rules;
using AiBehaviorLab.Simulation;

namespace AiBehaviorLab.Tests;

/// <summary>함수로 의도를 정하고, 받은 관찰을 모두 기록하는 Brain.</summary>
internal sealed class ScriptedBrain(Func<Observation, Intent> decide) : IBrain
{
    public List<Observation> Seen { get; } = [];

    public string Name => "Scripted";

    public static ScriptedBrain Idle() => new(_ => Intent.None);

    /// <summary>정한 틱에만 의도를 낸다.</summary>
    public static ScriptedBrain At(int tick, Intent intent) => new(o => o.Tick == tick ? intent : Intent.None);

    public void Reset(BrainContext context) => Seen.Clear();

    public Intent Decide(in Observation observation)
    {
        Seen.Add(observation);
        return decide(observation);
    }
}

internal static class TestSupport
{
    public static readonly Vector2 Up = new(0f, 1f);
    public static readonly Vector2 Right = new(1f, 0f);

    public static CombatRules MakeRules(Vector2 player, Vector2 boss) =>
        DefaultRules.V0() with { PlayerStart = player, BossStart = boss };

    public static CombatSimulation StartMatch(
        CombatRules rules, IBrain player, IBrain boss, HumanLimits? playerLimits = null, ulong seed = 1)
    {
        var setup = new MatchSetup(seed, player, boss)
        {
            PlayerLimits = playerLimits ?? HumanLimits.None,
            RecordEvents = true,
        };
        return new CombatSimulation(rules, setup);
    }

    public static void Steps(this CombatSimulation simulation, int count)
    {
        for (int i = 0; i < count; i++)
            simulation.Step();
    }
}
