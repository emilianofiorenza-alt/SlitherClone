namespace Slither.Core;

public readonly record struct BodyNode(WorldVector Position);

public sealed record SnakeState(
    ulong Tick,
    WorldVector HeadPosition,
    WorldVector Heading,
    WorldVector TargetHeading,
    double CurrentSpeed,
    IReadOnlyList<BodyNode> Body,
    double TargetLength,
    bool IsBoosting,
    int Energy,
    int TotalEnergy,
    double SizeScale);
