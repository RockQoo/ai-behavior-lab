using System.Numerics;

namespace AiBehaviorLab.Simulation;

/// <summary>FNV-1a 64비트. 실수는 비트 패턴 그대로 섞는다.</summary>
internal struct StateHasher
{
    private const ulong Prime = 1099511628211UL;

    public StateHasher()
    {
    }

    public ulong Value { get; private set; } = 14695981039346656037UL;

    public void Add(int value)
    {
        uint bits = unchecked((uint)value);
        for (int i = 0; i < 4; i++)
        {
            Value = (Value ^ (bits & 0xFF)) * Prime;
            bits >>= 8;
        }
    }

    public void Add(float value) => Add(BitConverter.SingleToInt32Bits(value));

    public void Add(Vector2 value)
    {
        Add(value.X);
        Add(value.Y);
    }
}
