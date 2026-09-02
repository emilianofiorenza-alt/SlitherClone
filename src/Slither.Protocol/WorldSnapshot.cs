namespace Slither.Protocol;

public readonly record struct BodyNodeSnapshot(double X, double Y, double Radius);

public readonly record struct SnakeSnapshot(
    double HeadX,
    double HeadY,
    double HeadingX,
    double HeadingY,
    double TargetHeadingX,
    double TargetHeadingY,
    double CurrentSpeed,
    double HeadRadius,
    IReadOnlyList<BodyNodeSnapshot> Body,
    double TargetLength,
    bool IsBoosting);

public readonly record struct ArenaSnapshot(
    double CenterX,
    double CenterY,
    double Radius,
    double PlayableRadius);

public readonly record struct WorldSnapshot(
    ulong SimulationTick,
    ArenaSnapshot Arena,
    SnakeSnapshot Snake);
