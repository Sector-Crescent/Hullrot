using Robust.Shared.Random;

namespace Content.Shared._Scp.Helpers;

[DataDefinition, Serializable]
public partial struct MinMaxExtended : IEquatable<MinMaxExtended>
{
    [DataField]
    public int Min;

    [DataField]
    public int Max;

    public MinMaxExtended(int min, int max)
    {
        Min = min;
        Max = max;
    }

    public readonly int Next(IRobustRandom random)
    {
        return random.Next(Min, Max + 1);
    }

    public readonly int Next(System.Random random)
    {
        return random.Next(Min, Max + 1);
    }

    public override readonly bool Equals(object? obj)
    {
        return obj is MinMaxExtended other && Equals(other);
    }

    public readonly bool Equals(MinMaxExtended other)
    {
        return Min == other.Min && Max == other.Max;
    }

    public override readonly int GetHashCode()
    {
        return HashCode.Combine(Min, Max);
    }

    public static bool operator ==(MinMaxExtended left, MinMaxExtended right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(MinMaxExtended left, MinMaxExtended right)
    {
        return !(left == right);
    }
}
