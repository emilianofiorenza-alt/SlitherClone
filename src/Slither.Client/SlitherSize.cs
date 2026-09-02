namespace Slither.Client;

public static class SlitherSize
{
    public const int PointsPerSegment = 10;

    public static int Calculate(int initialSegmentCount, int totalEnergy) =>
        (Math.Max(0, initialSegmentCount) * PointsPerSegment) + Math.Max(0, totalEnergy);
}
