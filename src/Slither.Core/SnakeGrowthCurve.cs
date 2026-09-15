namespace Slither.Core;

public static class SnakeGrowthCurve
{
    public const double MinimumScale = 0.8;
    public const double MaximumScale = 4.0;
    public const int GrowthStartScore = 50;
    public const double ScorePerScaleUnit = 2000.0;
    public const double CameraGrowthRatio = 0.35;

    public const double BasePointsPerSegment = 10.0;

    public static double ForScore(int score)
    {
        if (score <= GrowthStartScore) return MinimumScale;

        var scale = MinimumScale + ((score - GrowthStartScore) / ScorePerScaleUnit);
        return Math.Min(scale, MaximumScale);
    }

    public static double PointsPerSegment(int score) => BasePointsPerSegment * ForScore(score);

    public static double CameraScaleForBodyScale(double bodyScale) =>
        1.0 +
        (Math.Max(0, Math.Clamp(bodyScale, MinimumScale, MaximumScale) - 1.0) * CameraGrowthRatio);

    public static int BodyNodesForScore(int score, int minimumNodes = 1) =>
        Math.Max(minimumNodes, (int)Math.Floor(Math.Max(0, score) / PointsPerSegment(score)));
}
