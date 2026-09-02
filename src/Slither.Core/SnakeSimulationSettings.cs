namespace Slither.Core;

public sealed record SnakeSimulationSettings
{
    public static SnakeSimulationSettings Default { get; } = new();

    public double BaseSpeed { get; init; } = 5.0;

    public double BoostSpeed { get; init; } = 8.0;

    public double MaxTurnRateDegrees { get; init; } = 200.0;

    public double BoostMaxTurnRateDegrees { get; init; } = 170.0;

    public double HeadRadius { get; init; } = 0.425;

    public double BodyRadius { get; init; } = 0.39;

    public double BodySpacing { get; init; } = 0.43;

    public int InitialBodyNodes { get; init; } = 5;

    public int ConstraintIterations { get; init; } = 2;

    public double ArenaRadius { get; init; } = 100.0;

    public double BoundaryTurnRateDegrees { get; init; } = 320.0;
}
