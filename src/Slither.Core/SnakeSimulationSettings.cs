namespace Slither.Core;

public sealed record SnakeSimulationSettings
{
    public static SnakeSimulationSettings Default { get; } = new();

    public double BaseSpeed { get; init; } = 3.0;

    public double BoostSpeed { get; init; } = 9.0;

    public double BoostAcceleration { get; init; } = 18.0;

    public double BoostDeceleration { get; init; } = 24.0;

    public double MaxTurnRateDegrees { get; init; } = 320.0;

    public double BoostMaxTurnRateDegrees { get; init; } = 290.0;

    public double HeadRadius { get; init; } = 0.23684375;

    public double BodyRadius { get; init; } = 0.23684375;

    public double BodySpacing { get; init; } = 0.15;

    public int InitialBodyNodes { get; init; } = 5;

    public int ConstraintIterations { get; init; } = 2;

    public double BodyTrailRelaxationPerSecond { get; init; } = 5.5;

    public int BodyTrailRelaxationSpan { get; init; } = 4;

    public double ArenaRadius { get; init; } = 100.0;

    public double BoundaryTurnRateDegrees { get; init; } = 320.0;
}
