using System.Collections.Frozen;
using System.Numerics;

namespace AiBehaviorLab.Rules;

/// <summary>전투 규칙 v0. 숫자를 바꾸면 docs/combat-rules.md도 바꾼다.</summary>
public static class DefaultRules
{
    public static CombatRules V0(int tickRate = 30)
    {
        int T(double seconds) => CombatRules.ToTicks(seconds, tickRate);

        // 보스 판정은 모두 0.1초
        int strike = T(0.1);

        var actions = new List<ActionDefinition>
        {
            // ── 플레이어 ──
            new(ActionId.LightAttack, Side.Player,
                [new ActionStep(T(0.3), strike) { Shape = new SectorShape(1.5f, 90f), Damage = 5 }],
                T(0.4)) { StaminaCost = 10 },

            new(ActionId.HeavyAttack, Side.Player,
                [new ActionStep(T(0.8), strike) { Shape = new SectorShape(2.0f, 90f), Damage = 15 }],
                T(0.6)) { StaminaCost = 25 },

            new(ActionId.Roll, Side.Player,
                [new ActionStep(0, T(0.4)) { MoveDistance = 3f, InvulnerableTicks = T(0.25) }],
                T(0.15)) { StaminaCost = 30 },

            new(ActionId.Potion, Side.Player,
                [new ActionStep(T(1.0), 1) { Heal = 35 }],
                0) { UsesPotion = true },

            // ── 보스 기본 패턴 ──
            new(ActionId.BossSlam, Side.Boss,
                [new ActionStep(T(0.7), strike) { Shape = new CircleShape(3f, 2.5f), Damage = 30 }],
                T(1.2)),

            new(ActionId.BossSweep, Side.Boss,
                [new ActionStep(T(0.6), strike) { Shape = new SectorShape(3.5f, 120f), Damage = 25 }],
                T(1.0)),

            new(ActionId.BossCharge, Side.Boss,
                [new ActionStep(T(0.8), T(0.4)) { Shape = new LineShape(9f, 2f), Damage = 20, MoveDistance = 8f }],
                T(1.5)),

            new(ActionId.BossShockwave, Side.Boss,
                [new ActionStep(T(1.0), strike) { Shape = new CircleShape(0f, 5f), Damage = 20 }],
                T(0.8)),

            // ── 보스 변형 패턴 ──
            new(ActionId.BossSlamDelayed, Side.Boss,
                [new ActionStep(T(1.2), strike) { Shape = new CircleShape(3f, 2.5f), Damage = 30 }],
                T(1.2)) { VariantOf = ActionId.BossSlam },

            new(ActionId.BossSweepDouble, Side.Boss,
                [
                    new ActionStep(T(0.6), strike) { Shape = new SectorShape(3.5f, 120f), Damage = 25 },
                    new ActionStep(T(0.5), strike) { Shape = new SectorShape(3.5f, 120f), Damage = 25 },
                ],
                T(1.0)) { VariantOf = ActionId.BossSweep },

            new(ActionId.BossChargeShort, Side.Boss,
                [
                    new ActionStep(T(0.8), T(0.2)) { Shape = new LineShape(5f, 2f), Damage = 20, MoveDistance = 4f },
                    new ActionStep(T(0.5), strike)
                    {
                        Shape = new SectorShape(3.5f, 120f), Damage = 25, Aim = AimMode.TowardOpponent,
                    },
                ],
                T(1.5)) { VariantOf = ActionId.BossCharge },

            new(ActionId.BossShockwaveRing, Side.Boss,
                [new ActionStep(T(1.0), strike) { Shape = new RingShape(2.5f, 5f), Damage = 20 }],
                T(0.8)) { VariantOf = ActionId.BossShockwave },
        };

        var rules = new CombatRules
        {
            TickRate = tickRate,
            ArenaRadius = 10f,
            TimeLimitTicks = T(180),
            PlayerStart = new Vector2(0f, -5f),
            BossStart = new Vector2(0f, 4f),
            Player = new CombatantStats(MaxHp: 100, MoveSpeed: 4f, BodyRadius: 0.4f,
                MaxStamina: 100f, StaminaRegenPerSecond: 20f, Potions: 3),
            Boss = new CombatantStats(MaxHp: 600, MoveSpeed: 3f, BodyRadius: 1f,
                MaxStamina: 0f, StaminaRegenPerSecond: 0f, Potions: 0),
            Actions = actions.ToFrozenDictionary(a => a.Id),
        };
        rules.Validate();
        return rules;
    }
}
