using AiBehaviorLab.Rules;
using AiBehaviorLab.Simulation;

namespace AiBehaviorLab.Viewer;

/// <summary>화면에 보이는 한국어 이름.</summary>
public static class Names
{
    public static string Of(ActionId id) => ActionNames.Korean(id);

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
        "fsm-player" => "FSM 플레이어",
        "fsm-boss" => "FSM 보스",
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
