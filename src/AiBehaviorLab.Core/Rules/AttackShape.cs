using System.Numerics;
using AiBehaviorLab.Math;

namespace AiBehaviorLab.Rules;

/// <summary>
/// 공격 범위. 조준 기준점(origin)과 방향(direction, 단위 벡터)을 받아
/// 대상 몸(원)과 겹치는지 판정한다. 예고 표시와 실제 판정이 같은 모양을 쓴다.
/// </summary>
public abstract record AttackShape
{
    public abstract bool Hits(Vector2 origin, Vector2 direction, Vector2 target, float targetRadius);
}

/// <summary>기준점에서 방향으로 ForwardOffset만큼 앞에 중심이 있는 원.</summary>
public sealed record CircleShape(float ForwardOffset, float Radius) : AttackShape
{
    public Vector2 Center(Vector2 origin, Vector2 direction) => origin + direction * ForwardOffset;

    public override bool Hits(Vector2 origin, Vector2 direction, Vector2 target, float targetRadius)
    {
        float reach = Radius + targetRadius;
        return Vector2.DistanceSquared(Center(origin, direction), target) <= reach * reach;
    }
}

/// <summary>기준점이 꼭짓점인 부채꼴. AngleDegrees는 전체 각도.</summary>
public sealed record SectorShape(float Radius, float AngleDegrees) : AttackShape
{
    public override bool Hits(Vector2 origin, Vector2 direction, Vector2 target, float targetRadius)
    {
        Vector2 toTarget = target - origin;
        float distance = toTarget.Length();
        if (distance > Radius + targetRadius)
            return false;
        if (distance <= targetRadius)
            return true; // 몸이 꼭짓점을 덮고 있다

        // 몸 반지름만큼 각도 여유를 준다(근사).
        float halfAngle = AngleDegrees * 0.5f * (MathF.PI / 180f);
        float tolerance = MathF.Asin(MathF.Min(1f, targetRadius / distance));
        float cos = Vector2.Dot(direction, toTarget / distance);
        float angle = MathF.Acos(System.Math.Clamp(cos, -1f, 1f));
        return angle <= halfAngle + tolerance;
    }
}

/// <summary>기준점에서 방향으로 뻗은 직사각형(길이 Length, 폭 Width).</summary>
public sealed record LineShape(float Length, float Width) : AttackShape
{
    public override bool Hits(Vector2 origin, Vector2 direction, Vector2 target, float targetRadius)
    {
        Vector2 toTarget = target - origin;
        float along = Vector2.Dot(toTarget, direction);
        if (along < -targetRadius || along > Length + targetRadius)
            return false;
        float side = MathF.Abs(VectorMath.Cross(direction, toTarget));
        return side <= Width * 0.5f + targetRadius;
    }
}

/// <summary>기준점 중심의 고리. InnerRadius 안쪽에 몸 전체가 들어가 있으면 안전하다.</summary>
public sealed record RingShape(float InnerRadius, float OuterRadius) : AttackShape
{
    public override bool Hits(Vector2 origin, Vector2 direction, Vector2 target, float targetRadius)
    {
        float distance = Vector2.Distance(origin, target);
        return distance - targetRadius <= OuterRadius && distance + targetRadius >= InnerRadius;
    }
}
