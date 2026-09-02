namespace Slither.Protocol;

public readonly record struct PlayerCommand(
    uint Sequence,
    double TargetDirectionX,
    double TargetDirectionY,
    bool HasDirection,
    bool Boost);
