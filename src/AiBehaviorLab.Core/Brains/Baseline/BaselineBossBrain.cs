using AiBehaviorLab.Math;
using AiBehaviorLab.Rules;
using AiBehaviorLab.Simulation;

namespace AiBehaviorLab.Brains.Baseline;

/// <summary>
/// 기준선 보스: 다가가서 기본 패턴 중 하나를 무작위로 쓴다. 중거리에서는 가끔 돌진.
/// 공격이 끝나면 잠깐 쉰다. 의사결정 기법을 쓰지 않은 비교 기준이다.
/// </summary>
public sealed class BaselineBossBrain : IBrain
{
    private static readonly ActionId[] CloseRangePatterns =
        [ActionId.BossSlam, ActionId.BossSweep, ActionId.BossShockwave];

    private const float CloseRange = 3.5f;
    private const float ChargeMinRange = 5f;
    private const float ChargeMaxRange = 9f;
    private const double ChargeChancePerTick = 0.05;

    private DeterministicRandom _random = null!;
    private bool _wasBusy;
    private int _restUntilTick;

    public string Name => "Baseline Boss";

    public void Reset(BrainContext context)
    {
        _random = context.Random;
        _wasBusy = false;
        _restUntilTick = 0;
    }

    public Intent Decide(in Observation observation)
    {
        var self = observation.Self;
        if (self.IsBusy)
        {
            _wasBusy = true;
            return Intent.None;
        }

        if (_wasBusy)
        {
            _wasBusy = false;
            _restUntilTick = observation.Tick + observation.Rules.ToTicks(_random.Range(0.3f, 1.0f));
        }

        var toPlayer = observation.Opponent.Position - self.Position;
        float distance = toPlayer.Length();
        var direction = VectorMath.NormalizeOr(toPlayer, self.Facing);

        // 쉬는 동안에는 거리만 좁힌다.
        if (observation.Tick < _restUntilTick)
            return distance > CloseRange ? Intent.Move(direction) : Intent.None;

        if (distance is > ChargeMinRange and < ChargeMaxRange && _random.Chance(ChargeChancePerTick))
            return Intent.Use(ActionId.BossCharge, direction);

        if (distance > CloseRange)
            return Intent.Move(direction);

        var pattern = CloseRangePatterns[_random.NextInt(CloseRangePatterns.Length)];
        return Intent.Use(pattern, direction);
    }
}
