using AiBehaviorLab.Math;

namespace AiBehaviorLab.Simulation;

/// <summary>
/// 상대 상태 기록을 들고 있다가, 반응 지연만큼 지난 모습에 위치 오차를 더해 보여 준다.
/// </summary>
internal sealed class PerceptionFilter
{
    private readonly CombatantView[] _history;
    private readonly int _delayTicks;
    private readonly float _positionError;
    private readonly DeterministicRandom _random;
    private int _head = -1;
    private int _count;

    public PerceptionFilter(int delayTicks, float positionError, DeterministicRandom random)
    {
        if (delayTicks < 0) throw new ArgumentOutOfRangeException(nameof(delayTicks));
        _delayTicks = delayTicks;
        _positionError = positionError;
        _random = random;
        _history = new CombatantView[delayTicks + 1];
    }

    public void Record(in CombatantView view)
    {
        _head = (_head + 1) % _history.Length;
        _history[_head] = view;
        _count = System.Math.Min(_count + 1, _history.Length);
    }

    /// <summary>기록이 지연보다 짧으면 가장 오래된 기록을 보여 준다.</summary>
    public CombatantView Perceive()
    {
        if (_count == 0)
            throw new InvalidOperationException("기록이 없다");

        int back = System.Math.Min(_delayTicks, _count - 1);
        int index = (_head - back + _history.Length) % _history.Length;
        var view = _history[index];

        if (_positionError <= 0f)
            return view;

        var offset = _random.InsideUnitCircle() * _positionError;
        var action = view.Action is { } a ? a with { AimOrigin = a.AimOrigin + offset } : (ActionView?)null;
        return view with { Position = view.Position + offset, Action = action };
    }
}
