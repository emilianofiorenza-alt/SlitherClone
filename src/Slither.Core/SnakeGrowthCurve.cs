namespace Slither.Core;

public static class SnakeGrowthCurve
{
    public const double MinimumScale = 0.8;
    public const double InitialVisualScaleMultiplier = 1.375;
    public const double MaximumScale = 4.5;
    public const int GrowthStartScore = 50;
    public const double ScorePerScaleUnit = 2000.0;
    public const int LinearScaleEndScore = 2000;
    public const double AsymptoticScaleScoreSpan = 6000.0;
    public const double CameraGrowthRatio = 0.35;
    public const double MaximumCameraScale = 2.05;

    public const double BasePointsPerSegment = 10.0;
    public const double SegmentEnergyScaleExponent = 1.5;
    public const int EnergyDensityGrowthStartScore = 5000;
    public const double EnergyDensityScoreSpan = 15000.0;

    public static double ForScore(int score)
    {
        var energyAreaScale = EnergyAreaScaleForScore(score);
        if (score >= LinearScaleEndScore) return energyAreaScale;

        var fade = 1.0 -
                   (Math.Max(0, score - GrowthStartScore) /
                    (double)(LinearScaleEndScore - GrowthStartScore));
        var initialBonus = MinimumScale * (InitialVisualScaleMultiplier - 1.0);
        return energyAreaScale + (initialBonus * fade);
    }

    private static double EnergyAreaScaleForScore(int score)
    {
        if (score <= GrowthStartScore) return MinimumScale;

        var linearEndScale = MinimumScale +
                             ((LinearScaleEndScore - GrowthStartScore) / ScorePerScaleUnit);
        if (score <= LinearScaleEndScore)
            return MinimumScale + ((score - GrowthStartScore) / ScorePerScaleUnit);

        var progress = 1.0 - Math.Exp(
            -(score - LinearScaleEndScore) / AsymptoticScaleScoreSpan);
        return linearEndScale + ((MaximumScale - linearEndScale) * progress);
    }

    public static double PointsPerSegment(int score)
    {
        var scale = EnergyAreaScaleForScore(score);
        var densityMultiplier = score <= EnergyDensityGrowthStartScore
            ? 1.0
            : Math.Sqrt(1.0 +
                        ((score - EnergyDensityGrowthStartScore) / EnergyDensityScoreSpan));
        return BasePointsPerSegment *
               Math.Pow(scale, SegmentEnergyScaleExponent) *
               densityMultiplier;
    }

    public static double CameraScaleForBodyScale(double bodyScale) =>
        Math.Min(
            MaximumCameraScale,
            1.0 +
            (Math.Max(0, Math.Clamp(bodyScale, MinimumScale, MaximumScale) - 1.0) * CameraGrowthRatio));

    public static double CameraScaleForScore(int score) =>
        CameraScaleForBodyScale(EnergyAreaScaleForScore(score));

    public static int BodyNodesForScore(int score, int minimumNodes = 1) =>
        Math.Max(minimumNodes, (int)Math.Floor(Math.Max(0, score) / PointsPerSegment(score)));
}
