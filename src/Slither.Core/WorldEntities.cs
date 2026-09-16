namespace Slither.Core;

public readonly record struct SnakeId(int Value);

public enum SnakeControllerKind
{
    Human,
    WanderBot
}

public enum SnakeLifeState
{
    Alive,
    Dying,
    DeadWaitingRespawn,
    Respawning
}

public enum PopulationMode
{
    InteractionTest,
    PopulationTest,
    StressTest
}

public enum GameplayMode
{
    Standard,
    DebugLong
}

public enum CollisionKind
{
    HeadToBody,
    HeadToHead
}

public readonly record struct SnakeControl(
    double TargetDirectionX,
    double TargetDirectionY,
    bool HasDirection,
    bool Boost);

public readonly record struct CollisionEvent(
    ulong Tick,
    SnakeId VictimId,
    int VictimGeneration,
    SnakeId? KillerId,
    WorldVector ContactPoint,
    CollisionKind Kind);

public enum WorldEventKind
{
    SnakeDied,
    SnakeRespawned,
    DotCollected
}

public readonly record struct WorldEvent(
    ulong Tick,
    WorldEventKind Kind,
    SnakeId SnakeId,
    int Generation,
    SnakeId? OtherSnakeId = null,
    int Value = 0);

public sealed record SnakeEntityState(
    SnakeId Id,
    int Generation,
    SnakeControllerKind ControllerKind,
    SnakeLifeState LifeState,
    int StyleId,
    SnakeState Motion,
    int MatchScore,
    int AcquiredMass,
    int Kills,
    int Deaths,
    double LifeSeconds,
    double RespawnTime);

public sealed record RadarSnakeState(
    SnakeId Id,
    int Generation,
    bool IsHuman,
    WorldVector HeadPosition,
    IReadOnlyList<WorldVector> Body);

public sealed record WorldMetrics(
    int AliveSnakes,
    int WaitingSnakes,
    int TotalBodyNodes,
    int CollisionCandidates,
    int NarrowPhaseTests,
    long TotalDeaths,
    long TotalRespawns,
    long GeneratedMass,
    long SnakeMass,
    long DotMass,
    long ReleasedMass,
    long DestroyedMass,
    double AiMilliseconds,
    double MotionMilliseconds,
    double SpatialIndexMilliseconds,
    double CollisionMilliseconds,
    double SnapshotMilliseconds);
