namespace Slither.Protocol;

public readonly record struct PlayerCommand(
    uint Sequence,
    float TargetDirectionX,
    float TargetDirectionY,
    bool Boost);
