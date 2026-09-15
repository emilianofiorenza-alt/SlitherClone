namespace Slither.Core;

public static class SnakeInterest
{
    public static bool IntersectsCircle(
        WorldVector center,
        double radius,
        WorldVector head,
        IReadOnlyList<WorldVector> body,
        double snakeRadius)
    {
        if (!double.IsFinite(radius) || radius <= 0)
            throw new ArgumentOutOfRangeException(nameof(radius));
        if (!double.IsFinite(snakeRadius) || snakeRadius < 0)
            throw new ArgumentOutOfRangeException(nameof(snakeRadius));

        var expandedRadius = radius + snakeRadius;
        var expandedRadiusSquared = expandedRadius * expandedRadius;
        if (DistanceSquared(head, center) <= expandedRadiusSquared) return true;
        foreach (var node in body)
            if (DistanceSquared(node, center) <= expandedRadiusSquared) return true;
        return false;
    }

    private static double DistanceSquared(WorldVector first, WorldVector second)
    {
        var delta = first - second;
        return (delta.X * delta.X) + (delta.Y * delta.Y);
    }
}
