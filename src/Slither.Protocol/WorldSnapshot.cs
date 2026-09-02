namespace Slither.Protocol;

public readonly record struct WorldSnapshot(
    ulong SimulationTick,
    float MarkerX,
    float MarkerY,
    float MarkerAngle);
