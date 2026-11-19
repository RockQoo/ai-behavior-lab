using AiBehaviorLab.Simulation;

namespace AiBehaviorLab.Brains.Fsm;

/// <summary>FSM의 상태 하나. 들어갈 때, 머무는 매 틱, 나갈 때 불린다.</summary>
public interface IState
{
    void Enter(in Observation observation)
    {
    }

    Intent Tick(in Observation observation);

    void Exit(in Observation observation)
    {
    }
}

/// <summary>함수로 만드는 상태. Brain 안에서 상태를 짧게 정의할 때 쓴다.</summary>
public sealed class DelegateState(Func<Observation, Intent> tick, Action<Observation>? enter = null) : IState
{
    public void Enter(in Observation observation) => enter?.Invoke(observation);

    public Intent Tick(in Observation observation) => tick(observation);
}

/// <summary>
/// 전이 하나. From이 null이면 어느 상태에서든 걸린다(인터럽트). Reason은 판단 근거 패널에 보인다.
/// 문턱값은 Condition 밖의 매개변수로 두면 3단계에서 진화시킬 수 있다.
/// </summary>
public sealed record Transition<TState>(TState? From, TState To, string Reason, Func<Observation, bool> Condition)
    where TState : struct, Enum;

/// <summary>
/// 열거형 상태를 쓰는 유한 상태 기계.
/// 한 틱에 전이는 최대 한 번이고, 검사 순서는 "어느 상태에서든" 전이(등록 순서) → 지금 상태의 전이(등록 순서)다.
/// </summary>
public sealed class StateMachine<TState> where TState : struct, Enum
{
    private readonly Dictionary<TState, IState> _states = new();
    private readonly List<Transition<TState>> _anyTransitions = new();
    private readonly List<Transition<TState>> _transitions = new();
    private TState _initial;
    private bool _entered;

    public StateMachine(TState initial)
    {
        _initial = initial;
        Current = initial;
    }

    public TState Current { get; private set; }

    /// <summary>지금 상태에서 이미 보낸 틱 수. 전이 조건과 상태의 Tick 안에서는 "이번 틱 전까지" 값이다.</summary>
    public int TicksInState { get; private set; }

    /// <summary>마지막 전이. 한 번도 안 했으면 null.</summary>
    public Transition<TState>? LastTransition { get; private set; }

    public int LastTransitionTick { get; private set; } = -1;

    public StateMachine<TState> State(TState id, IState state)
    {
        _states[id] = state;
        return this;
    }

    public StateMachine<TState> On(TState from, TState to, string reason, Func<Observation, bool> condition)
    {
        _transitions.Add(new Transition<TState>(from, to, reason, condition));
        return this;
    }

    public StateMachine<TState> FromAny(TState to, string reason, Func<Observation, bool> condition)
    {
        _anyTransitions.Add(new Transition<TState>(null, to, reason, condition));
        return this;
    }

    /// <summary>처음 상태로 되돌린다. 다음 Tick에서 처음 상태의 Enter가 불린다.</summary>
    public void Reset(TState initial)
    {
        _initial = initial;
        Current = initial;
        TicksInState = 0;
        LastTransition = null;
        LastTransitionTick = -1;
        _entered = false;
    }

    public Intent Tick(in Observation observation)
    {
        if (!_entered)
        {
            Current = _initial;
            StateOf(Current).Enter(observation);
            _entered = true;
        }

        if (FindTransition(observation) is { } transition)
        {
            StateOf(Current).Exit(observation);
            Current = transition.To;
            TicksInState = 0;
            LastTransition = transition;
            LastTransitionTick = observation.Tick;
            StateOf(Current).Enter(observation);
        }

        var intent = StateOf(Current).Tick(observation);
        TicksInState++;
        return intent;
    }

    private Transition<TState>? FindTransition(in Observation observation)
    {
        foreach (var transition in _anyTransitions)
        {
            if (!EqualityComparer<TState>.Default.Equals(transition.To, Current) && transition.Condition(observation))
                return transition;
        }

        foreach (var transition in _transitions)
        {
            if (EqualityComparer<TState>.Default.Equals(transition.From!.Value, Current) && transition.Condition(observation))
                return transition;
        }

        return null;
    }

    private IState StateOf(TState id)
    {
        return _states.TryGetValue(id, out var state)
            ? state
            : throw new InvalidOperationException($"등록하지 않은 상태: {id}");
    }
}
