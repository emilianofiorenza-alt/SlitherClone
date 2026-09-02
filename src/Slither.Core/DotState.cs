namespace Slither.Core;

public readonly record struct DotState(
    long Id,
    WorldVector Position,
    double Radius,
    int Energy);

public readonly record struct CellCoordinate(int X, int Y);
