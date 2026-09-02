namespace Slither.Core;

public readonly record struct WorldVector(double X, double Y)
{
    public double Length => Math.Sqrt((X * X) + (Y * Y));

    public WorldVector NormalizedOr(WorldVector fallback)
    {
        var length = Length;
        return length > 1e-12 && double.IsFinite(length)
            ? new WorldVector(X / length, Y / length)
            : fallback;
    }

    public static WorldVector operator +(WorldVector left, WorldVector right) =>
        new(left.X + right.X, left.Y + right.Y);

    public static WorldVector operator -(WorldVector left, WorldVector right) =>
        new(left.X - right.X, left.Y - right.Y);

    public static WorldVector operator *(WorldVector vector, double scalar) =>
        new(vector.X * scalar, vector.Y * scalar);
}
