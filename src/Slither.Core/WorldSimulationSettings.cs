namespace Slither.Core;

public sealed record WorldSimulationSettings
{
    public static WorldSimulationSettings Default { get; } = new();

    public int WorldSeed { get; init; } = 260902;

    public double DotCellSize { get; init; } = 10.0;

    public int DotsPerCell { get; init; } = 27;

    public int ActiveCellRadius { get; init; } = 2;

    public double DotViewportPreloadMargin { get; init; } = 4.0;

    public double NewDotFadeInSeconds { get; init; } = 0.30;

    public double SmallDotRadius { get; init; } = 0.13;

    public double MediumDotRadius { get; init; } = 0.195;

    public double LargeDotRadius { get; init; } = 0.27;

    public double MediumDotProbability { get; init; } = 0.24;

    public double LargeDotProbability { get; init; } = 0.09;

    public double DynamicSpawnIntervalSeconds { get; init; } = 2.0;

    public int DynamicSpawnCount { get; init; } = 4;

    public double DynamicSpawnMinimumRadius { get; init; } = 3.0;

    public double DynamicSpawnMaximumRadius { get; init; } = 9.0;

    public int EnergyPerSegment { get; init; } = 10;

    public int GrowthPerDot { get; init; } = 1;

    public int GrowthMassPerSegment { get; init; } = 5;

    public double BoostEnergyReleaseFraction { get; init; } = 0.005;

    public double BoostEnergyReleaseIntervalSeconds { get; init; } = 0.30;

    public int BoostMinimumScore { get; init; } = 50;

    public int BoostMaximumDotEnergy { get; init; } = 3;

    public double BoostDropTrailSpacingScale { get; init; } = 1.35;

    public double BoostDropOrthogonalSpreadScale { get; init; } = 1.0;

    public double BoostDropFadeInSeconds { get; init; } = 0.10;

    public int InitialMatchScore { get; init; } = 50;

    public int InitialBotMinimumScore { get; init; } = 100;

    public int InitialBotMaximumScore { get; init; } = 2000;

    public GameplayMode StartupGameplayMode { get; init; } = GameplayMode.Standard;

    public int DebugLongInitialMatchScore { get; init; } = 10000;

    public int InitialScorePointsPerBodyNode { get; init; } = 10;

    public int InitialBotCount { get; init; } = 10;

    public double VisibleSnakeRadius { get; init; } = 18.0;

    public double RadarUpdateIntervalSeconds { get; init; } = 1.0;

    public double InitialBotMinimumRadius { get; init; } = 8.0;

    public double InitialBotMaximumRadius { get; init; } = 25.0;

    public double InitialHeadClearance { get; init; } = 2.0;

    public double BotDirectionMinimumSeconds { get; init; } = 0.7;

    public double BotDirectionMaximumSeconds { get; init; } = 2.5;

    public double BotMaximumDeviationDegrees { get; init; } = 35.0;

    public double BotInputTurnRateDegrees { get; init; } = 120.0;

    public double BotBoostStartsPerSecond { get; init; } = 0.02;

    public double BotBoostMinimumSeconds { get; init; } = 0.3;

    public double BotBoostMaximumSeconds { get; init; } = 1.0;

    public double BotBoundaryMargin { get; init; } = 12.0;

    public double BotDotSenseRadius { get; init; } = 8.0;

    public double BotDotAttractionWeight { get; init; } = 0.65;

    public double BotOpponentAvoidanceRadius { get; init; } = 2.5;

    public double BotOpponentAvoidanceWeight { get; init; } = 2.8;

    public double BotAvoidanceLookAhead { get; init; } = 0.9;

    public double BotPerceptionIntervalSeconds { get; init; } = 0.20;

    public double CollisionGridCellSize { get; init; } = 2.0;

    public double HeadCollisionRadiusScale { get; init; } = 1.0;

    public double BodyCollisionRadiusScale { get; init; } = 1.0;

    public bool HeadToHeadCollisionEnabled { get; init; } = true;

    public double HeadToHeadApproachTieTolerance { get; init; } = 0.15;

    public double DropFraction { get; init; } = 0.80;

    public double DeathDropRadiusScale { get; init; } = 1.45;

    public double DeathDropCountScale { get; init; } = 0.70;

    public double DeathDropSmallProbability { get; init; } = 0.20;

    public double DeathDropSmallMinimumRadiusScale { get; init; } = 0.45;

    public double DeathDropSmallMaximumRadiusScale { get; init; } = 0.75;

    public double DeathDropMainMinimumRadiusScale { get; init; } = 0.88;

    public double DeathDropLongitudinalSpreadScale { get; init; } = 0.42;

    public double DeathDropOrthogonalSpreadScale { get; init; } = 1.10;

    public double DeathDropFadeInSeconds { get; init; } = 0.35;

    public double BotRespawnSeconds { get; init; } = 1.0;

    public double PlayerDeathObservationSeconds { get; init; } = 3.0;

    public double PlayerDeathFadeSeconds { get; init; } = 0.65;

    public double RespawnHeadClearance { get; init; } = 6.0;

    public double RespawnBodyClearance { get; init; } = 3.0;

    public double RespawnBoundaryMargin { get; init; } = 15.0;

    public int RespawnAttempts { get; init; } = 32;
}
