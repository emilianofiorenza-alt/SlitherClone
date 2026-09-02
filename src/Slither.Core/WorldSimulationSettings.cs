namespace Slither.Core;

public sealed record WorldSimulationSettings
{
    public static WorldSimulationSettings Default { get; } = new();

    public int WorldSeed { get; init; } = 260902;

    public double DotCellSize { get; init; } = 10.0;

    public int DotsPerCell { get; init; } = 27;

    public int ActiveCellRadius { get; init; } = 2;

    public double SmallDotRadius { get; init; } = 0.13;

    public double MediumDotRadius { get; init; } = 0.195;

    public double LargeDotRadius { get; init; } = 0.27;

    public double MediumDotProbability { get; init; } = 0.24;

    public double LargeDotProbability { get; init; } = 0.09;

    public double DynamicSpawnIntervalSeconds { get; init; } = 2.0;

    public int DynamicSpawnCount { get; init; } = 4;

    public double DynamicSpawnMinimumRadius { get; init; } = 3.0;

    public double DynamicSpawnMaximumRadius { get; init; } = 9.0;

    public int EnergyPerSegment { get; init; } = 5;
}
