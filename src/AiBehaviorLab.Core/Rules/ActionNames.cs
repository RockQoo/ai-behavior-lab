namespace AiBehaviorLab.Rules;

/// <summary>행동의 한국어 이름. 판단 근거 글과 관전 화면이 같이 쓴다.</summary>
public static class ActionNames
{
    public static string Korean(ActionId id) => id switch
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
}
