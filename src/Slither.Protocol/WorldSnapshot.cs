namespace Slither.Protocol;

public readonly record struct BodyNodeSnapshot(double X, double Y, double Radius);

public readonly record struct DotSnapshot(
    long Id,
    double X,
    double Y,
    double Radius,
    int Energy,
    double Opacity = 1.0);

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
    bool IsBoosting,
    int Energy,
    int Size,
    int Id = 1,
    int Generation = 0,
    int StyleId = 0,
    bool IsHuman = true,
    int MatchScore = 50,
    int Kills = 0,
    int Deaths = 0,
    bool IsAlive = true,
    double SizeScale = 1.0);

public readonly record struct ArenaSnapshot(
    double CenterX,
    double CenterY,
    double Radius,
    double PlayableRadius);

public readonly record struct RadarBodyNodeSnapshot(double X, double Y);

public readonly record struct RadarSnakeSnapshot(
    int Id,
    int Generation,
    bool IsHuman,
    double HeadX,
    double HeadY,
    IReadOnlyList<RadarBodyNodeSnapshot> Body);

public readonly record struct WorldSnapshot(
    ulong SimulationTick,
    ArenaSnapshot Arena,
    SnakeSnapshot Snake,
    IReadOnlyList<DotSnapshot> VisibleDots,
    int ActiveCellCount,
    int CollectedDotCount,
    IReadOnlyList<SnakeSnapshot>? VisibleSnakes = null,
    IReadOnlyList<WorldEventSnapshot>? Events = null,
    WorldMetricsSnapshot Metrics = default,
    int ConfiguredBotCount = 0,
    int PopulationMode = 0,
    IReadOnlyList<RadarSnakeSnapshot>? RadarSnakes = null);

public readonly record struct WorldEventSnapshot(
    ulong Tick,
    int Kind,
    int SnakeId,
    int Generation,
    int? OtherSnakeId,
    int Value);

public readonly record struct WorldMetricsSnapshot(
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
