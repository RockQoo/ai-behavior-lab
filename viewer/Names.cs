using AiBehaviorLab.Rules;
using AiBehaviorLab.Simulation;

namespace AiBehaviorLab.Viewer;

/// <summary>화면에 보이는 한국어 이름.</summary>
public static class Names
{
    public static string Of(ActionId id) => id switch
    {
        ActionId.LightAttack => "평타",
        ActionId.HeavyAttack => "강공격",
        ActionId.Roll => "구르기",
        ActionId.Potion => "회복약",
        ActionId.BossSlam => "내려찍기",
        ActionId.BossSweep => "휩쓸기",
        ActionId.BossCharge => "돌진",
        ActionId.BossShockwave => "충격파",
        ActionId.BossSlamDelayed => "지연 내려찍기",
        ActionId.BossSweepDouble => "2연타 휩쓸기",
        ActionId.BossChargeShort => "짧은 돌진",
        ActionId.BossShockwaveRing => "고리 충격파",
        _ => id.ToString(),
    };

    public static string Of(ActionPhase phase) => phase switch
    {
        ActionPhase.Windup => "예고",
        ActionPhase.Active => "판정",
        ActionPhase.Recovery => "후딜",
        _ => phase.ToString(),
    };

    public static string Of(Side side) => side == Side.Player ? "플레이어" : "보스";

    public static string Of(EndReason reason) => reason switch
    {
        EndReason.PlayerDefeated => "플레이어 쓰러짐",
        EndReason.BossDefeated => "보스 쓰러짐",
        EndReason.Timeout => "시간 초과",
        _ => reason.ToString(),
    };

    public static string Brain(string name) => name switch
    {
        "baseline-player" => "기준선 플레이어",
        "baseline-boss" => "기준선 보스",
        "idle" => "가만히",
        _ => name,
    };

    public static string Limits(HumanLimits limits)
    {
        if (limits == HumanLimits.None) return "사람 한계 없음";
        if (limits == HumanLimits.Casual) return "일반 유저";
        if (limits == HumanLimits.Expert) return "고인물";
        return $"반응 {limits.ReactionSeconds:F2}s";
    }
}
