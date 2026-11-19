using AiBehaviorLab.Math;
using AiBehaviorLab.Rules;
using AiBehaviorLab.Simulation;

namespace AiBehaviorLab.Brains.Baseline;

/// <summary>
/// 기준선 플레이어: 예고 범위 안이면 걸어서 벗어나다가, 예고가 거의 끝나면 기준점 반대쪽으로 구른다.
/// HP가 낮고 여유가 있으면 회복약, 보스 후딜에 사거리 안이면 평타, 아니면 다가간다. 의사결정 기법을 쓰지 않은 비교 기준이다.
/// </summary>
/// <param name="rollLeadTicks">
/// 보이는 예고 남은 시간이 이 틱 수 이하가 되면 구른다. 반응 지연만큼 늦게 보이므로
/// 기본값 12는 "일반 유저"(지연 9틱)·"고인물"(지연 5틱)에서 무적(8틱)이 판정을 덮도록 맞춘 값이다.
/// </param>
public sealed class BaselinePlayerBrain(int rollLeadTicks = 12) : IBrain
{
    private const float PotionHpRatio = 0.4f;
    private const float SafePotionDistance = 6f;
    private const float RangeMargin = 0.2f;

    public string Name => "Baseline Player";

    public void Reset(BrainContext context)
    {
    }

    public Intent Decide(in Observation observation)
    {
        var self = observation.Self;
        if (self.IsBusy)
            return Intent.None;

        var rules = observation.Rules;
        var boss = observation.Opponent;
        var toBoss = boss.Position - self.Position;
        float distance = toBoss.Length();
        var towardBoss = VectorMath.NormalizeOr(toBoss, self.Facing);

        // 1. 예고 범위 안이면 걸어서 벗어나다가, 판정 직전에 구른다.
        if (boss.Action is { } threat && Telegraph.Threatens(rules, threat, self.Position, self.BodyRadius))
        {
            var away = VectorMath.NormalizeOr(self.Position - threat.AimOrigin, -towardBoss);
            bool strikeSoon = threat.Phase == ActionPhase.Active || threat.PhaseRemaining <= rollLeadTicks;
            return strikeSoon && self.Stamina >= rules.Get(ActionId.Roll).StaminaCost
                ? Intent.Use(ActionId.Roll, away)
                : Intent.Move(away);
        }

        // 2. HP가 낮고, 멀리 있거나 보스 후딜이 회복약 시간보다 길면 마신다.
        if (self.Hp <= self.MaxHp * PotionHpRatio && self.Potions > 0)
        {
            int potionTicks = rules.Get(ActionId.Potion).Steps[0].WindupTicks;
            bool bossRecovering = boss.Action is { Phase: ActionPhase.Recovery } recovery
                                  && recovery.PhaseRemaining >= potionTicks;
            if (distance > SafePotionDistance || bossRecovering)
                return Intent.Use(ActionId.Potion, towardBoss);
        }

        // 3. 보스 후딜(반격 기회)에 사거리 안이면 평타. 그 밖에는 사거리 근처에서 기다린다.
        //    아무 때나 치면 후딜에 묶여서 다음 공격을 피하지 못한다.
        var light = rules.Get(ActionId.LightAttack);
        var lightStep = light.Steps[0];
        float reach = ((SectorShape)lightStep.Shape!).Radius + boss.BodyRadius - RangeMargin;
        bool punishWindow = boss.Action is { Phase: ActionPhase.Recovery };

        if (distance > reach)
            return Intent.Move(towardBoss);
        if (punishWindow && self.Stamina >= light.StaminaCost)
            return Intent.Use(ActionId.LightAttack, towardBoss);
        return Intent.None;
    }
}
