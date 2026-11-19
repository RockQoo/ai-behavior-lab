using System.Numerics;
using AiBehaviorLab.Rules;

namespace AiBehaviorLab.Simulation;

/// <summary>예고(선딜)와 판정 중인 공격 범위를 묻는 도우미. 예고 범위 = 판정 범위.</summary>
public static class Telegraph
{
    /// <summary>지금 단계의 공격 범위. 후딜이거나 공격하지 않는 단계면 null.</summary>
    public static AttackShape? CurrentShape(CombatRules rules, in ActionView action)
    {
        if (action.Phase == ActionPhase.Recovery)
            return null;
        var step = rules.Get(action.Id).Steps[action.StepIndex];
        return step.Damage > 0 ? step.Shape : null;
    }

    /// <summary>지금 단계의 공격 범위가 이 위치의 몸과 겹치는가.</summary>
    public static bool Threatens(CombatRules rules, in ActionView action, Vector2 position, float bodyRadius)
    {
        var shape = CurrentShape(rules, action);
        return shape is not null && shape.Hits(action.AimOrigin, action.AimDirection, position, bodyRadius);
    }
}
