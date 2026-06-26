namespace Kantonal.Domain;

public readonly record struct BfsNumber(int Value)
{
    public static BfsNumber Create(int value)
    {
        if (value <= 0)
            throw new ArgumentOutOfRangeException(nameof(value), value, "BFS number must be positive.");
        return new BfsNumber(value);
    }
}
