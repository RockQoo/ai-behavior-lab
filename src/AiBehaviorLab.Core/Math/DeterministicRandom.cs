using System.Numerics;

namespace AiBehaviorLab.Math;

/// <summary>
/// 시드가 같으면 항상 같은 수열을 내는 난수(SplitMix64).
/// 시뮬레이션 안에서는 System.Random 대신 이것만 쓴다.
/// </summary>
public sealed class DeterministicRandom
{
    private ulong _state;

    public DeterministicRandom(ulong seed)
    {
        _state = seed;
    }

    /// <summary>시드와 흐름 번호로 서로 독립적인 새 시드를 만든다. 용도별로 난수 흐름을 나눌 때 쓴다.</summary>
    public static ulong Derive(ulong seed, ulong stream)
    {
        return Mix(seed ^ Mix(stream + 0x9E3779B97F4A7C15UL));
    }

    public ulong NextULong()
    {
        _state += 0x9E3779B97F4A7C15UL;
        return Mix(_state);
    }

    /// <summary>[0, 1)</summary>
    public double NextDouble()
    {
        return (NextULong() >> 11) * (1.0 / (1UL << 53));
    }

    /// <summary>[0, maxExclusive)</summary>
    public int NextInt(int maxExclusive)
    {
        if (maxExclusive <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxExclusive));
        return (int)(NextDouble() * maxExclusive);
    }

    /// <summary>[min, max)</summary>
    public float Range(float min, float max)
    {
        return min + (float)NextDouble() * (max - min);
    }

    public bool Chance(double probability)
    {
        return NextDouble() < probability;
    }

    /// <summary>반지름 1인 원 안의 균등한 점.</summary>
    public Vector2 InsideUnitCircle()
    {
        float angle = Range(0f, MathF.Tau);
        float radius = MathF.Sqrt((float)NextDouble());
        return new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
    }

    private static ulong Mix(ulong z)
    {
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
