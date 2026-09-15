namespace Slither.Core;

public static class DeathDropPath
{
    public static WorldVector Sample(SnakeState snake, int dropIndex, int dropCount)
    {
        if (dropCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(dropCount));
        }
        if (dropIndex < 0 || dropIndex >= dropCount)
        {
            throw new ArgumentOutOfRangeException(nameof(dropIndex));
        }

        var normalizedPosition = dropCount == 1
            ? 0.5
            : dropIndex / (double)(dropCount - 1);
        var lastPathIndex = snake.Body.Count;
        var continuousIndex = normalizedPosition * lastPathIndex;
        var lowerIndex = Math.Clamp((int)Math.Floor(continuousIndex), 0, lastPathIndex);
        var upperIndex = Math.Min(lowerIndex + 1, lastPathIndex);
        var amount = continuousIndex - lowerIndex;
        var lower = PointAt(snake, lowerIndex);
        var upper = PointAt(snake, upperIndex);
        return lower + ((upper - lower) * amount);
    }

    private static WorldVector PointAt(SnakeState snake, int pathIndex) =>
        pathIndex == 0 ? snake.HeadPosition : snake.Body[pathIndex - 1].Position;
}
