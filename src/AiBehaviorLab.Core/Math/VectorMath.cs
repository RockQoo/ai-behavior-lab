using System.Numerics;

namespace AiBehaviorLab.Math;

public static class VectorMath
{
    /// <summary>길이가 0에 가까우면 fallback을 돌려준다.</summary>
    public static Vector2 NormalizeOr(Vector2 v, Vector2 fallback)
    {
        float lengthSquared = v.LengthSquared();
        return lengthSquared > 1e-8f ? v / MathF.Sqrt(lengthSquared) : fallback;
    }

    /// <summary>2D 외적(z 성분).</summary>
    public static float Cross(Vector2 a, Vector2 b)
    {
        return a.X * b.Y - a.Y * b.X;
    }

    public static Vector2 Rotate(Vector2 v, float radians)
    {
        float cos = MathF.Cos(radians);
        float sin = MathF.Sin(radians);
        return new Vector2(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);
    }

    /// <summary>왼쪽으로 90도 돌린 벡터.</summary>
    public static Vector2 PerpendicularLeft(Vector2 v)
    {
        return new Vector2(-v.Y, v.X);
    }
}
