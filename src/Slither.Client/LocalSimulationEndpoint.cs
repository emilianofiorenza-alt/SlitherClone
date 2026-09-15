using Slither.Core;
using Slither.Protocol;

namespace Slither.Client;

public sealed class LocalSimulationEndpoint : ISimulationEndpoint
{
    private readonly WorldSimulation _simulation = new();
    private PlayerCommand _pendingCommand;
    private IReadOnlyList<RadarSnakeState>? _radarSource;
    private IReadOnlyList<RadarSnakeSnapshot> _radarSnapshots = Array.Empty<RadarSnakeSnapshot>();

    public void Submit(in PlayerCommand command) => _pendingCommand = command;

    public void Step(double fixedDeltaTime)
    {
        _simulation.Step(
            fixedDeltaTime,
            _pendingCommand.TargetDirectionX,
            _pendingCommand.TargetDirectionY,
            _pendingCommand.HasDirection,
            _pendingCommand.Boost);
    }

    public void ConfigurePopulation(PopulationMode mode, int botCount) =>
        _simulation.ConfigurePopulation(mode, botCount);

    public void ConfigureGameplayMode(GameplayMode mode) =>
        _simulation.ConfigureGameplayMode(mode);

    public WorldSnapshot CaptureSnapshot()
    {
        var state = _simulation.CaptureState();
        var snakes = new SnakeSnapshot[state.VisibleSnakes.Count];
        for (var snakeIndex = 0; snakeIndex < snakes.Length; snakeIndex++)
            snakes[snakeIndex] = MapSnake(state.VisibleSnakes[snakeIndex]);
        var localSnake = snakes.First(snake => snake.Id == state.LocalSnakeId.Value);

        var dots = new DotSnapshot[state.ActiveDots.Count];
        for (var index = 0; index < dots.Length; index++)
        {
            var dot = state.ActiveDots[index];
            var opacity = dot.FadeInDurationTicks == 0
                ? 1.0
                : Math.Clamp(
                    (state.Tick - Math.Min(state.Tick, dot.FadeInStartTick)) /
                    (double)dot.FadeInDurationTicks,
                    0,
                    1);
            dots[index] = new DotSnapshot(
                dot.Id,
                dot.Position.X,
                dot.Position.Y,
                dot.Radius,
                dot.Energy,
                opacity);
        }

        if (!ReferenceEquals(_radarSource, state.RadarSnakes))
        {
            var radarSnakes = new RadarSnakeSnapshot[state.RadarSnakes.Count];
            for (var snakeIndex = 0; snakeIndex < radarSnakes.Length; snakeIndex++)
            {
                var radarSnake = state.RadarSnakes[snakeIndex];
                var radarBody = new RadarBodyNodeSnapshot[radarSnake.Body.Count];
                for (var nodeIndex = 0; nodeIndex < radarBody.Length; nodeIndex++)
                {
                    var node = radarSnake.Body[nodeIndex];
                    radarBody[nodeIndex] = new RadarBodyNodeSnapshot(node.X, node.Y);
                }
                radarSnakes[snakeIndex] = new RadarSnakeSnapshot(
                    radarSnake.Id.Value,
                    radarSnake.Generation,
                    radarSnake.IsHuman,
                    radarSnake.HeadPosition.X,
                    radarSnake.HeadPosition.Y,
                    radarBody);
            }
            _radarSource = state.RadarSnakes;
            _radarSnapshots = radarSnakes;
        }

        return new WorldSnapshot(
            state.Tick,
            new ArenaSnapshot(
                0,
                0,
                _simulation.SnakeSettings.ArenaRadius,
                _simulation.SnakeSettings.ArenaRadius - localSnake.HeadRadius),
            localSnake,
            dots,
            state.ActiveCellCount,
            state.CollectedDotCount,
            snakes,
            state.Events.Select(static item => new WorldEventSnapshot(
                item.Tick, (int)item.Kind, item.SnakeId.Value, item.Generation, item.OtherSnakeId?.Value, item.Value)).ToArray(),
            new WorldMetricsSnapshot(
                state.Metrics.AliveSnakes, state.Metrics.WaitingSnakes, state.Metrics.TotalBodyNodes,
                state.Metrics.CollisionCandidates, state.Metrics.NarrowPhaseTests, state.Metrics.TotalDeaths,
                state.Metrics.TotalRespawns, state.Metrics.GeneratedMass, state.Metrics.SnakeMass,
                state.Metrics.DotMass, state.Metrics.ReleasedMass,
                state.Metrics.DestroyedMass, state.Metrics.AiMilliseconds, state.Metrics.MotionMilliseconds,
                state.Metrics.SpatialIndexMilliseconds, state.Metrics.CollisionMilliseconds,
                state.Metrics.SnapshotMilliseconds),
            state.ConfiguredBotCount,
            (int)state.PopulationMode,
            _radarSnapshots);
    }

    private SnakeSnapshot MapSnake(SnakeEntityState entity)
    {
        var snakeState = entity.Motion;
        var body = new BodyNodeSnapshot[snakeState.Body.Count];
        for (var index = 0; index < body.Length; index++)
        {
            var node = snakeState.Body[index];
            body[index] = new BodyNodeSnapshot(
                node.Position.X,
                node.Position.Y,
                _simulation.SnakeSettings.BodyRadius * snakeState.SizeScale);
        }
        return new SnakeSnapshot(
            snakeState.HeadPosition.X, snakeState.HeadPosition.Y, snakeState.Heading.X, snakeState.Heading.Y,
            snakeState.TargetHeading.X, snakeState.TargetHeading.Y, snakeState.CurrentSpeed,
            _simulation.SnakeSettings.HeadRadius * snakeState.SizeScale, body, snakeState.TargetLength, snakeState.IsBoosting,
            snakeState.Energy, entity.MatchScore, entity.Id.Value, entity.Generation, entity.StyleId,
            entity.ControllerKind == SnakeControllerKind.Human, entity.MatchScore, entity.Kills, entity.Deaths,
            entity.LifeState == SnakeLifeState.Alive, snakeState.SizeScale);
    }
}
