using System.Diagnostics;

namespace Slither.Core;

public sealed class WorldSimulation
{
    private const int HumanSnakeId = 1;
    private readonly WorldSimulationSettings _settings;
    private readonly SnakeSimulationSettings _snakeSettings;
    private readonly DotField _dots;
    private readonly SnakeCollisionDetector _collisionDetector;
    private readonly List<SnakeEntity> _snakes = [];
    private readonly List<WorldEvent> _events = [];
    private ulong _randomState;
    private double _dynamicSpawnTime;
    private long _totalDeaths;
    private long _totalRespawns;
    private long _releasedMass;
    private long _destroyedMass;
    private long _debugGrantedMass;
    private double _aiMilliseconds;
    private double _motionMilliseconds;
    private double _indexMilliseconds;
    private double _collisionMilliseconds;
    private IReadOnlyList<RadarSnakeState> _cachedRadarSnakes = Array.Empty<RadarSnakeState>();
    private double _radarUpdateElapsed;
    private bool _radarSnapshotDirty = true;

    public WorldSimulation(SnakeSimulationSettings? snakeSettings = null, WorldSimulationSettings? worldSettings = null)
    {
        _settings = worldSettings ?? WorldSimulationSettings.Default;
        _snakeSettings = snakeSettings ?? SnakeSimulationSettings.Default;
        ValidateSettings(_settings);
        _dots = new DotField(_snakeSettings.ArenaRadius, _settings);
        _collisionDetector = new SnakeCollisionDetector(_settings.CollisionGridCellSize);
        _randomState = Mix((ulong)(uint)_settings.WorldSeed ^ 0xA0761D6478BD642FUL);
        GameplayMode = _settings.StartupGameplayMode;
        AddSnake(new SnakeId(HumanSnakeId), SnakeControllerKind.Human, new WorldVector(0, 0), new WorldVector(1, 0));
        ConfigurePopulation(PopulationMode.InteractionTest, _settings.InitialBotCount);
        _dots.ActivateAround(new WorldVector(0, 0));
    }

    public SnakeSimulationSettings SnakeSettings => _snakeSettings;
    public WorldSimulationSettings Settings => _settings;
    public PopulationMode PopulationMode { get; private set; } = PopulationMode.InteractionTest;
    public GameplayMode GameplayMode { get; private set; }
    public int ConfiguredBotCount { get; private set; }
    public ulong Tick { get; private set; }

    public void ConfigureGameplayMode(GameplayMode mode)
    {
        GameplayMode = mode;
        var human = _snakes[0];
        var position = human.Simulation.HeadPosition;
        var heading = human.Simulation.Heading;
        human.Generation++;
        human.Simulation = new SnakeSimulation(PlayerSnakeSettings(), position, heading);
        human.MatchScore = PlayerInitialScore();
        _destroyedMass += human.AcquiredMass;
        human.AcquiredMass = PlayerInitialAcquiredMass();
        _debugGrantedMass += human.AcquiredMass;
        ApplyGrowthScale(human);
        human.LifeSeconds = 0;
        human.LifeState = SnakeLifeState.Alive;
        _radarSnapshotDirty = true;
        _events.Add(new WorldEvent(Tick, WorldEventKind.SnakeRespawned, human.Id, human.Generation));
    }

    public void ConfigurePopulation(PopulationMode mode, int botCount)
    {
        if (botCount is < 0 or > 250) throw new ArgumentOutOfRangeException(nameof(botCount));
        PopulationMode = mode;
        ConfiguredBotCount = botCount;
        while (_snakes.Count - 1 > botCount) _snakes.RemoveAt(_snakes.Count - 1);
        while (_snakes.Count - 1 < botCount)
        {
            var id = new SnakeId(_snakes.Count + 1);
            AddSnake(id, SnakeControllerKind.WanderBot, FindInitialSpawn(mode), RandomDirection());
        }
        _radarSnapshotDirty = true;
    }

    public void Step(double fixedDeltaTime, double requestedDirectionX, double requestedDirectionY, bool hasDirection, bool boost)
    {
        if (!double.IsFinite(fixedDeltaTime) || fixedDeltaTime <= 0) throw new ArgumentOutOfRangeException(nameof(fixedDeltaTime));
        Tick++;
        _radarUpdateElapsed += fixedDeltaTime;
        if (_radarUpdateElapsed >= _settings.RadarUpdateIntervalSeconds)
        {
            _radarUpdateElapsed %= _settings.RadarUpdateIntervalSeconds;
            _radarSnapshotDirty = true;
        }
        _events.Clear();
        var humanControl = new SnakeControl(requestedDirectionX, requestedDirectionY, hasDirection, boost);

        var timer = Stopwatch.StartNew();
        var controls = new SnakeControl[_snakes.Count];
        for (var index = 0; index < _snakes.Count; index++)
        {
            var entity = _snakes[index];
            if (entity.LifeState != SnakeLifeState.Alive) continue;
            if (entity.ControllerKind == SnakeControllerKind.Human)
            {
                controls[index] = humanControl;
                continue;
            }

            entity.PerceptionTime -= fixedDeltaTime;
            if (entity.PerceptionTime <= 0)
            {
                entity.PerceptionTime += _settings.BotPerceptionIntervalSeconds;
                _dots.EnsureAround(entity.Simulation.HeadPosition);
                entity.DotTarget = _dots.TryFindNearest(
                    entity.Simulation.HeadPosition,
                    _settings.BotDotSenseRadius,
                    out var nearestDot)
                    ? nearestDot.Position
                    : null;
                entity.Avoidance = CalculateOpponentAvoidance(entity);
            }
            controls[index] = entity.Bot!.Sample(
                fixedDeltaTime,
                entity.Simulation.HeadPosition,
                entity.Simulation.Heading,
                _snakeSettings.ArenaRadius,
                entity.DotTarget,
                entity.Avoidance);
        }
        _aiMilliseconds = timer.Elapsed.TotalMilliseconds;

        timer.Restart();
        for (var index = 0; index < _snakes.Count; index++)
        {
            var entity = _snakes[index];
            if (entity.LifeState != SnakeLifeState.Alive) continue;
            var control = controls[index];
            entity.Simulation.Step(fixedDeltaTime, control.TargetDirectionX, control.TargetDirectionY, control.HasDirection, control.Boost);
            entity.LifeSeconds += fixedDeltaTime;
        }
        _motionMilliseconds = timer.Elapsed.TotalMilliseconds;

        CollectDots();
        ReplenishDots(fixedDeltaTime);
        timer.Restart();
        RebuildCollisionIndex();
        _indexMilliseconds = timer.Elapsed.TotalMilliseconds;
        timer.Restart();
        ResolveDeaths(DetectCollisions());
        _collisionMilliseconds = timer.Elapsed.TotalMilliseconds;
        UpdateRespawns(fixedDeltaTime);
    }

    public WorldState CaptureState()
    {
        var timer = Stopwatch.StartNew();
        var local = _snakes[0];
        var localPosition = local.LifeState == SnakeLifeState.Alive ? local.Simulation.HeadPosition : new WorldVector(0, 0);
        var visibleRadius = _settings.VisibleSnakeRadius * local.Simulation.SizeScale;
        var visible = new List<SnakeEntityState>();
        List<RadarSnakeState>? refreshedRadar = _radarSnapshotDirty
            ? new List<RadarSnakeState>(_snakes.Count)
            : null;
        var alive = 0;
        var waiting = 0;
        var bodyNodes = 0;
        foreach (var entity in _snakes)
        {
            if (entity.LifeState != SnakeLifeState.Alive)
            {
                waiting++;
                if (entity.Id.Value == HumanSnakeId) visible.Add(entity.Capture(entity.Simulation.CaptureState()));
                continue;
            }
            alive++;
            bodyNodes += entity.Simulation.BodyPositions.Count;
            if (refreshedRadar is not null)
            {
                refreshedRadar.Add(new RadarSnakeState(
                    entity.Id,
                    entity.Generation,
                    entity.ControllerKind == SnakeControllerKind.Human,
                    entity.Simulation.HeadPosition,
                    entity.Simulation.BodyPositions.ToArray()));
            }
            if (entity.Id.Value == HumanSnakeId ||
                SnakeInterest.IntersectsCircle(
                    localPosition,
                    visibleRadius,
                    entity.Simulation.HeadPosition,
                    entity.Simulation.BodyPositions,
                    entity.Simulation.BodyRadius))
                visible.Add(entity.Capture(entity.Simulation.CaptureState()));
        }
        if (refreshedRadar is not null)
        {
            _cachedRadarSnakes = refreshedRadar.ToArray();
            _radarSnapshotDirty = false;
        }
        visible.Sort(static (left, right) => left.Id.Value.CompareTo(right.Id.Value));
        var activeDots = _dots.ActivateAround(localPosition);
        var snakeMass = _snakes.Sum(static snake => (long)snake.AcquiredMass);
        var metrics = new WorldMetrics(alive, waiting, bodyNodes, _collisionDetector.CandidateCount,
            _collisionDetector.NarrowPhaseCount, _totalDeaths, _totalRespawns,
            _dots.EnvironmentalMassGenerated + _debugGrantedMass,
            snakeMass, _dots.ActiveEnergy, _releasedMass, _destroyedMass, _aiMilliseconds, _motionMilliseconds, _indexMilliseconds,
            _collisionMilliseconds, timer.Elapsed.TotalMilliseconds);
        return new WorldState(Tick, new SnakeId(HumanSnakeId), visible, _cachedRadarSnakes, activeDots, _dots.GeneratedCellCount,
            _dots.CollectedCount, _events.ToArray(), metrics, PopulationMode, ConfiguredBotCount);
    }

    private void AddSnake(SnakeId id, SnakeControllerKind kind, WorldVector position, WorldVector heading)
    {
        var matchScore = kind == SnakeControllerKind.Human
            ? PlayerInitialScore()
            : RandomBotInitialScore();
        var simulationSettings = SnakeSettingsForInitialScore(matchScore);
        var entity = new SnakeEntity(id, kind, id.Value % 8, new SnakeSimulation(simulationSettings, position, heading),
            kind == SnakeControllerKind.WanderBot ? new WanderBot(_settings.WorldSeed, id, 0, heading, _settings) : null,
            matchScore);
        ApplyGrowthScale(entity);
        entity.AcquiredMass = InitialAcquiredMass(matchScore);
        _debugGrantedMass += entity.AcquiredMass;
        _snakes.Add(entity);
    }

    private WorldVector FindInitialSpawn(PopulationMode mode)
    {
        var maximumRadius = _snakeSettings.ArenaRadius - _settings.RespawnBoundaryMargin;
        var best = new WorldVector(0, 0);
        var bestClearance = double.NegativeInfinity;
        for (var attempt = 0; attempt < _settings.RespawnAttempts; attempt++)
        {
            var minimum = mode == PopulationMode.InteractionTest ? _settings.InitialBotMinimumRadius : 0;
            var maximum = mode == PopulationMode.InteractionTest ? Math.Min(_settings.InitialBotMaximumRadius, maximumRadius) : maximumRadius;
            var radius = Math.Sqrt(Lerp(minimum * minimum, maximum * maximum, NextDouble()));
            var angle = NextDouble() * Math.PI * 2;
            var candidate = new WorldVector(Math.Cos(angle) * radius, Math.Sin(angle) * radius);
            var clearance = MinimumHeadClearance(candidate);
            if (clearance > bestClearance) { best = candidate; bestClearance = clearance; }
            if (clearance >= _settings.InitialHeadClearance) return candidate;
        }
        return best;
    }

    private void CollectDots()
    {
        foreach (var entity in _snakes.OrderBy(static snake => snake.Id.Value))
        {
            if (entity.LifeState != SnakeLifeState.Alive) continue;
            var collected = _dots.CollectDetailedAt(entity.Simulation.HeadPosition, entity.Simulation.HeadRadius);
            if (collected.Count == 0) continue;
            entity.MatchScore += collected.Score;
            entity.AcquiredMass += collected.Score;
            ApplyGrowthScale(entity);
            entity.Simulation.EnsureBodyNodeCount(
                SnakeGrowthCurve.BodyNodesForScore(entity.MatchScore, _snakeSettings.InitialBodyNodes));
            _events.Add(new WorldEvent(Tick, WorldEventKind.DotCollected, entity.Id, entity.Generation, Value: collected.Score));
        }
    }

    private void ReplenishDots(double fixedDeltaTime)
    {
        _dynamicSpawnTime += fixedDeltaTime;
        while (_dynamicSpawnTime >= _settings.DynamicSpawnIntervalSeconds)
        {
            _dynamicSpawnTime -= _settings.DynamicSpawnIntervalSeconds;
            var alive = _snakes.Where(static snake => snake.LifeState == SnakeLifeState.Alive).ToArray();
            if (alive.Length > 0)
            {
                var selected = alive[(int)(Tick % (ulong)alive.Length)].Simulation.HeadPosition;
                _dots.SpawnNear(selected, _settings.DynamicSpawnCount);
            }
        }
        foreach (var entity in _snakes)
            if (entity.LifeState == SnakeLifeState.Alive) _dots.EnsureAround(entity.Simulation.HeadPosition);
    }

    private void RebuildCollisionIndex()
    {
        var bodies = new List<CollisionBody>();
        foreach (var entity in _snakes)
        {
            if (entity.LifeState != SnakeLifeState.Alive) continue;
            var radius = entity.Simulation.BodyRadius * _settings.BodyCollisionRadiusScale;
            var body = entity.Simulation.BodyPositions;
            for (var index = 0; index < body.Count; index++)
                bodies.Add(new CollisionBody(entity.Id, entity.Generation, body[index], body[index], radius, index));
        }
        _collisionDetector.BeginTick();
        _collisionDetector.Rebuild(bodies);
    }

    private List<CollisionEvent> DetectCollisions()
    {
        var collisions = new List<CollisionEvent>();
        var alive = _snakes.Where(static snake => snake.LifeState == SnakeLifeState.Alive).ToArray();
        var headToHeadPairs = new HashSet<(int First, int Second)>();
        if (_settings.HeadToHeadCollisionEnabled)
        {
            for (var first = 0; first < alive.Length; first++)
            {
                var firstPosition = alive[first].Simulation.HeadPosition;
                for (var second = first + 1; second < alive.Length; second++)
                {
                    var secondPosition = alive[second].Simulation.HeadPosition;
                    var delta = firstPosition - secondPosition;
                    var collisionRadius =
                        (alive[first].Simulation.HeadRadius + alive[second].Simulation.HeadRadius) *
                        _settings.HeadCollisionRadiusScale;
                    if ((delta.X * delta.X) + (delta.Y * delta.Y) > collisionRadius * collisionRadius) continue;

                    headToHeadPairs.Add(PairKey(alive[first].Id, alive[second].Id));
                    var result = HeadToHeadResolver.Resolve(
                        new HeadToHeadContestant(
                            alive[first].Id,
                            firstPosition,
                            alive[first].Simulation.Heading,
                            alive[first].MatchScore),
                        new HeadToHeadContestant(
                            alive[second].Id,
                            secondPosition,
                            alive[second].Simulation.Heading,
                            alive[second].MatchScore),
                        _settings.HeadToHeadApproachTieTolerance);
                    var contactPoint = (firstPosition + secondPosition) * 0.5;
                    if (result.FirstDies)
                    {
                        SnakeId? killer = result.SecondDies ? null : alive[second].Id;
                        collisions.Add(new CollisionEvent(Tick, alive[first].Id, alive[first].Generation, killer, contactPoint, CollisionKind.HeadToHead));
                    }
                    if (result.SecondDies)
                    {
                        SnakeId? killer = result.FirstDies ? null : alive[first].Id;
                        collisions.Add(new CollisionEvent(Tick, alive[second].Id, alive[second].Generation, killer, contactPoint, CollisionKind.HeadToHead));
                    }
                }
            }
        }

        foreach (var entity in alive)
        {
            var head = entity.Simulation.HeadPosition;
            var killer = _collisionDetector
                .Query(new CollisionHead(entity.Id, entity.Generation, head, 0))
                .Where(hit => !headToHeadPairs.Contains(PairKey(entity.Id, hit.OwnerId)))
                .OrderBy(static hit => hit.OwnerId.Value)
                .FirstOrDefault();
            if (killer.OwnerId.Value != 0)
                collisions.Add(new CollisionEvent(Tick, entity.Id, entity.Generation, killer.OwnerId, head, CollisionKind.HeadToBody));
        }
        return collisions.OrderBy(static collision => collision.VictimId.Value).ThenBy(static collision => collision.Kind).ToList();
    }

    private static (int First, int Second) PairKey(SnakeId first, SnakeId second) =>
        first.Value < second.Value ? (first.Value, second.Value) : (second.Value, first.Value);

    private void ResolveDeaths(IReadOnlyList<CollisionEvent> collisions)
    {
        var uniqueVictims = new HashSet<(SnakeId Id, int Generation)>();
        foreach (var collision in collisions)
        {
            if (!uniqueVictims.Add((collision.VictimId, collision.VictimGeneration))) continue;
            var victim = _snakes.First(snake => snake.Id == collision.VictimId);
            if (victim.LifeState != SnakeLifeState.Alive || victim.Generation != collision.VictimGeneration) continue;
            victim.LifeState = SnakeLifeState.Dying;
            victim.Deaths++;
            if (collision.KillerId is { } killerId)
            {
                var killer = _snakes.FirstOrDefault(snake => snake.Id == killerId);
                if (killer is not null) killer.Kills++;
            }
            CreateDeathDrops(victim);
            victim.LifeState = SnakeLifeState.DeadWaitingRespawn;
            victim.RespawnTime = victim.ControllerKind == SnakeControllerKind.Human ? _settings.PlayerRespawnSeconds : _settings.BotRespawnSeconds;
            _totalDeaths++;
            _events.Add(new WorldEvent(Tick, WorldEventKind.SnakeDied, victim.Id, victim.Generation, collision.KillerId));
        }
    }

    private void CreateDeathDrops(SnakeEntity victim)
    {
        var releasedEnergy = victim.AcquiredMass;
        var state = victim.Simulation.CaptureState();
        var dotCount = state.Body.Count;
        if (releasedEnergy <= 0 || dotCount == 0) return;

        _releasedMass += releasedEnergy;
        victim.AcquiredMass = 0;
        for (var index = 0; index < dotCount; index++)
        {
            var bodyPosition = state.Body[index].Position;
            var previousPosition = index == 0
                ? state.HeadPosition
                : state.Body[index - 1].Position;
            var nextPosition = index + 1 < dotCount
                ? state.Body[index + 1].Position
                : bodyPosition - state.Heading;
            var tangent = (previousPosition - nextPosition).NormalizedOr(state.Heading);
            var normal = new WorldVector(-tangent.Y, tangent.X);
            var longitudinalOffset = ((NextDouble() * 2) - 1) *
                                     victim.Simulation.BodyRadius *
                                     _settings.DeathDropLongitudinalSpreadScale;
            var orthogonalOffset = ((NextDouble() * 2) - 1) *
                                   victim.Simulation.BodyRadius *
                                   _settings.DeathDropOrthogonalSpreadScale;
            var radius = victim.Simulation.BodyRadius * _settings.DeathDropRadiusScale;
            var position = ClampInsideArena(
                bodyPosition + (tangent * longitudinalOffset) + (normal * orthogonalOffset),
                radius);

            _dots.AddDropDot(
                position,
                DeathDropEnergy.ForIndex(releasedEnergy, dotCount, index),
                1,
                Tick,
                (ulong)Math.Max(1, Math.Ceiling(
                    _settings.DeathDropFadeInSeconds * SimulationSettings.TicksPerSecond)),
                radius);
        }
    }

    private WorldVector ClampInsideArena(WorldVector position, double radius)
    {
        var maximumDistance = Math.Max(0, _snakeSettings.ArenaRadius - radius - 1e-6);
        return position.Length <= maximumDistance
            ? position
            : position.NormalizedOr(new WorldVector(1, 0)) * maximumDistance;
    }

    private void UpdateRespawns(double fixedDeltaTime)
    {
        foreach (var entity in _snakes)
        {
            if (entity.LifeState != SnakeLifeState.DeadWaitingRespawn) continue;
            entity.RespawnTime -= fixedDeltaTime;
            if (entity.RespawnTime > 0) continue;
            entity.LifeState = SnakeLifeState.Respawning;
            var position = FindSafeRespawn();
            var heading = RandomDirection();
            entity.Generation++;
            var initialScore = entity.ControllerKind == SnakeControllerKind.Human
                ? PlayerInitialScore()
                : RandomBotInitialScore();
            var simulationSettings = SnakeSettingsForInitialScore(initialScore);
            entity.Simulation = new SnakeSimulation(simulationSettings, position, heading);
            entity.Bot = entity.ControllerKind == SnakeControllerKind.WanderBot ? new WanderBot(_settings.WorldSeed, entity.Id, entity.Generation, heading, _settings) : null;
            entity.MatchScore = initialScore;
            entity.AcquiredMass = InitialAcquiredMass(initialScore);
            _debugGrantedMass += entity.AcquiredMass;
            ApplyGrowthScale(entity);
            entity.LifeSeconds = 0;
            entity.PerceptionTime = 0;
            entity.DotTarget = null;
            entity.Avoidance = new WorldVector(0, 0);
            entity.LifeState = SnakeLifeState.Alive;
            _totalRespawns++;
            _events.Add(new WorldEvent(Tick, WorldEventKind.SnakeRespawned, entity.Id, entity.Generation));
        }
    }

    private WorldVector FindSafeRespawn()
    {
        var radiusLimit = _snakeSettings.ArenaRadius - _settings.RespawnBoundaryMargin;
        var best = new WorldVector(0, 0);
        var bestClearance = double.NegativeInfinity;
        for (var attempt = 0; attempt < _settings.RespawnAttempts; attempt++)
        {
            var radius = Math.Sqrt(NextDouble()) * radiusLimit;
            var angle = NextDouble() * Math.PI * 2;
            var candidate = new WorldVector(Math.Cos(angle) * radius, Math.Sin(angle) * radius);
            var clearance = MinimumHeadClearance(candidate);
            if (clearance > bestClearance) { best = candidate; bestClearance = clearance; }
            if (clearance >= _settings.RespawnHeadClearance && MinimumBodyClearance(candidate) >= _settings.RespawnBodyClearance) return candidate;
        }
        return best;
    }

    private int PlayerInitialScore() => GameplayMode == Slither.Core.GameplayMode.DebugLong
        ? _settings.DebugLongInitialMatchScore
        : _settings.InitialMatchScore;

    private SnakeSimulationSettings PlayerSnakeSettings() => SnakeSettingsForInitialScore(PlayerInitialScore());

    private SnakeSimulationSettings SnakeSettingsForInitialScore(int score) => _snakeSettings with
    {
        InitialBodyNodes = SnakeGrowthCurve.BodyNodesForScore(score, _snakeSettings.InitialBodyNodes)
    };

    private int PlayerInitialAcquiredMass() => InitialAcquiredMass(PlayerInitialScore());

    private static int InitialAcquiredMass(int score) => score;

    private int RandomBotInitialScore() =>
        _settings.InitialBotMinimumScore +
        (int)Math.Floor(NextDouble() * ((_settings.InitialBotMaximumScore - _settings.InitialBotMinimumScore) + 1));

    private static void ApplyGrowthScale(SnakeEntity entity) =>
        entity.Simulation.SetSizeScale(SnakeGrowthCurve.ForScore(entity.MatchScore));

    private WorldVector CalculateOpponentAvoidance(SnakeEntity observer)
    {
        var lookAhead = observer.Simulation.HeadPosition +
                        (observer.Simulation.Heading * _settings.BotAvoidanceLookAhead);
        var radius = _settings.BotOpponentAvoidanceRadius;
        var avoidance = new WorldVector(0, 0);
        foreach (var other in _snakes)
        {
            if (other.Id == observer.Id || other.LifeState != SnakeLifeState.Alive)
            {
                continue;
            }

            avoidance += RepulsionFrom(lookAhead, other.Simulation.HeadPosition, radius);
            foreach (var node in other.Simulation.BodyPositions)
            {
                avoidance += RepulsionFrom(lookAhead, node, radius);
            }
        }
        return avoidance.NormalizedOr(new WorldVector(0, 0));
    }

    private static WorldVector RepulsionFrom(WorldVector position, WorldVector obstacle, double radius)
    {
        var delta = position - obstacle;
        var distance = delta.Length;
        if (distance <= double.Epsilon || distance >= radius)
        {
            return new WorldVector(0, 0);
        }
        return (delta * (1.0 / distance)) * (1.0 - (distance / radius));
    }

    private double MinimumHeadClearance(WorldVector candidate)
    {
        var minimum = double.PositiveInfinity;
        foreach (var entity in _snakes)
            if (entity.LifeState == SnakeLifeState.Alive) minimum = Math.Min(minimum, (entity.Simulation.HeadPosition - candidate).Length);
        return minimum;
    }

    private double MinimumBodyClearance(WorldVector candidate)
    {
        var minimum = double.PositiveInfinity;
        foreach (var entity in _snakes)
        {
            if (entity.LifeState != SnakeLifeState.Alive) continue;
            foreach (var node in entity.Simulation.BodyPositions) minimum = Math.Min(minimum, (node - candidate).Length);
        }
        return minimum;
    }

    private WorldVector RandomDirection()
    {
        var angle = NextDouble() * Math.PI * 2;
        return new WorldVector(Math.Cos(angle), Math.Sin(angle));
    }

    private double NextDouble()
    {
        _randomState += 0x9E3779B97F4A7C15UL;
        var value = Mix(_randomState);
        return (value >> 11) * (1.0 / (1UL << 53));
    }

    private static double Lerp(double start, double end, double amount) => start + ((end - start) * amount);
    private static ulong Mix(ulong value)
    {
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }

    private static void ValidateSettings(WorldSimulationSettings settings)
    {
        if (settings.InitialBotCount < 0 || settings.InitialMatchScore < 1 ||
            settings.InitialBotMinimumScore < settings.InitialMatchScore ||
            settings.InitialBotMaximumScore < settings.InitialBotMinimumScore ||
            settings.DebugLongInitialMatchScore < settings.InitialMatchScore ||
            settings.InitialScorePointsPerBodyNode < 1 ||
            settings.VisibleSnakeRadius <= 0 || settings.RadarUpdateIntervalSeconds <= 0 || settings.InitialBotMinimumRadius < 0 ||
            settings.InitialBotMaximumRadius < settings.InitialBotMinimumRadius || settings.BotDirectionMinimumSeconds <= 0 ||
            settings.BotDirectionMaximumSeconds < settings.BotDirectionMinimumSeconds || settings.CollisionGridCellSize <= 0 ||
            settings.DropFraction is < 0 or > 1 || settings.DeathDropRadiusScale <= 0 ||
            settings.DeathDropSmallProbability is < 0 or > 1 ||
            settings.DeathDropSmallMinimumRadiusScale <= 0 ||
            settings.DeathDropSmallMaximumRadiusScale < settings.DeathDropSmallMinimumRadiusScale ||
            settings.DeathDropMainMinimumRadiusScale < settings.DeathDropSmallMaximumRadiusScale ||
            settings.DeathDropMainMinimumRadiusScale > 1 ||
            settings.DeathDropLongitudinalSpreadScale <= 0 || settings.DeathDropOrthogonalSpreadScale <= 0 ||
            settings.DeathDropFadeInSeconds <= 0 ||
            settings.BotDotSenseRadius <= 0 || settings.BotOpponentAvoidanceRadius <= 0 ||
            settings.BotInputTurnRateDegrees <= 0 ||
            settings.HeadToHeadApproachTieTolerance is < 0 or > 2 ||
            settings.BotOpponentAvoidanceWeight < 0 ||
            settings.BotPerceptionIntervalSeconds <= 0 ||
            settings.RespawnAttempts < 1)
            throw new ArgumentOutOfRangeException(nameof(settings), "Step 3 world settings contain invalid values.");
    }

    private sealed class SnakeEntity
    {
        public SnakeEntity(SnakeId id, SnakeControllerKind controllerKind, int styleId, SnakeSimulation simulation, WanderBot? bot, int matchScore)
        { Id = id; ControllerKind = controllerKind; StyleId = styleId; Simulation = simulation; Bot = bot; MatchScore = matchScore; }
        public SnakeId Id { get; }
        public int Generation { get; set; }
        public SnakeControllerKind ControllerKind { get; }
        public SnakeLifeState LifeState { get; set; } = SnakeLifeState.Alive;
        public int StyleId { get; }
        public SnakeSimulation Simulation { get; set; }
        public WanderBot? Bot { get; set; }
        public int MatchScore { get; set; }
        public int AcquiredMass { get; set; }
        public int Kills { get; set; }
        public int Deaths { get; set; }
        public double LifeSeconds { get; set; }
        public double RespawnTime { get; set; }
        public double PerceptionTime { get; set; }
        public WorldVector? DotTarget { get; set; }
        public WorldVector Avoidance { get; set; }
        public SnakeEntityState Capture(SnakeState state) => new(Id, Generation, ControllerKind, LifeState, StyleId, state,
            MatchScore, AcquiredMass, Kills, Deaths, LifeSeconds);
    }
}

public sealed record WorldState(
    ulong Tick,
    SnakeId LocalSnakeId,
    IReadOnlyList<SnakeEntityState> VisibleSnakes,
    IReadOnlyList<RadarSnakeState> RadarSnakes,
    IReadOnlyList<DotState> ActiveDots,
    int ActiveCellCount,
    int CollectedDotCount,
    IReadOnlyList<WorldEvent> Events,
    WorldMetrics Metrics,
    PopulationMode PopulationMode,
    int ConfiguredBotCount);
