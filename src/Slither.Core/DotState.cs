namespace Slither.Core;

public readonly record struct DotState(
    long Id,
    WorldVector Position,
    double Radius,
    int Energy,
    ulong FadeInStartTick = 0,
    ulong FadeInDurationTicks = 0);

public readonly record struct CellCoordinate(int X, int Y);
