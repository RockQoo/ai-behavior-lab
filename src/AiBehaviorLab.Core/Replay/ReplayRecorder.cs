using AiBehaviorLab.Rules;
using AiBehaviorLab.Simulation;

namespace AiBehaviorLab.Replay;

/// <summary>한 판을 돌리면서 매 틱 상태를 기록한다.</summary>
public static class ReplayRecorder
{
    public static ReplayFile Record(MatchSpec spec)
    {
        var rules = ReplayCatalog.CreateRules(spec.RulesName);
        var simulation = CreateSimulation(spec, rules);

        var frames = new List<ReplayFrame>(rules.TimeLimitTicks + 1) { Capture(simulation, 0) };
        while (!simulation.IsOver)
        {
            int eventStart = simulation.Events.Count;
            simulation.Step();
            frames.Add(Capture(simulation, eventStart));
        }

        var result = simulation.Result!;
        var header = new ReplayHeader(
            ReplayFile.CurrentFormatVersion, spec, rules.TickRate, rules.ArenaRadius,
            result.Winner, result.Reason, result.Ticks);
        return new ReplayFile(header, frames);
    }

    /// <summary>스펙대로 판을 만든다(사건 기록 켬). 검증에서도 같은 방법을 쓴다.</summary>
    public static CombatSimulation CreateSimulation(MatchSpec spec, CombatRules rules)
    {
        var setup = new MatchSetup(spec.Seed,
            ReplayCatalog.CreateBrain(spec.PlayerBrain), ReplayCatalog.CreateBrain(spec.BossBrain))
        {
            PlayerLimits = spec.PlayerLimits,
            BossLimits = spec.BossLimits,
            RecordEvents = true,
        };
        return new CombatSimulation(rules, setup);
    }

    public static string FormatHash(ulong hash) => hash.ToString("x16");

    private static ReplayFrame Capture(CombatSimulation simulation, int eventStart)
    {
        var events = new List<EventFrame>();
        for (int i = eventStart; i < simulation.Events.Count; i++)
        {
            var e = simulation.Events[i];
            events.Add(new EventFrame(e.Kind, e.Source, e.Action, e.Value));
        }

        return new ReplayFrame(
            simulation.Tick,
            Body(simulation.Rules, simulation.Player),
            Body(simulation.Rules, simulation.Boss),
            events,
            FormatHash(simulation.ComputeStateHash()));
    }

    private static BodyFrame Body(CombatRules rules, CombatantView view)
    {
        ActionFrame? action = null;
        ShapeFrame? shape = null;
        if (view.Action is { } a)
        {
            action = new ActionFrame(a.Id, a.StepIndex, a.Phase, a.PhaseElapsed, a.PhaseLength);
            if (Telegraph.CurrentShape(rules, a) is { } s)
                shape = Shape(s, a);
        }

        return new BodyFrame(
            view.Position.X, view.Position.Y, view.Facing.X, view.Facing.Y, view.BodyRadius,
            view.Hp, view.MaxHp, view.Stamina, view.Potions, view.IsInvulnerable, action, shape);
    }

    private static ShapeFrame Shape(AttackShape shape, ActionView action)
    {
        var (kind, a, b) = shape switch
        {
            CircleShape c => (ShapeKind.Circle, c.ForwardOffset, c.Radius),
            SectorShape s => (ShapeKind.Sector, s.Radius, s.AngleDegrees),
            LineShape l => (ShapeKind.Line, l.Length, l.Width),
            RingShape r => (ShapeKind.Ring, r.InnerRadius, r.OuterRadius),
            _ => throw new NotSupportedException($"리플레이에 기록할 수 없는 범위: {shape.GetType().Name}"),
        };
        return new ShapeFrame(kind, action.AimOrigin.X, action.AimOrigin.Y, action.AimDirection.X,
            action.AimDirection.Y, a, b, action.Phase == ActionPhase.Active);
    }
}
