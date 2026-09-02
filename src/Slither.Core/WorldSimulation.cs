namespace Slither.Core;

public sealed class WorldSimulation
{
    private readonly WorldSimulationSettings _settings;
    private readonly SnakeSimulation _snake;
    private readonly DotField _dots;
    private double _dynamicSpawnTime;

    public WorldSimulation(
        SnakeSimulationSettings? snakeSettings = null,
        WorldSimulationSettings? worldSettings = null)
    {
        _settings = worldSettings ?? WorldSimulationSettings.Default;
        _snake = new SnakeSimulation(snakeSettings);
        _dots = new DotField(_snake.Settings.ArenaRadius, _settings);
        _dots.ActivateAround(_snake.CaptureState().HeadPosition);
    }

    public SnakeSimulationSettings SnakeSettings => _snake.Settings;

    public WorldSimulationSettings Settings => _settings;

    public void Step(
        double fixedDeltaTime,
        double requestedDirectionX,
        double requestedDirectionY,
        bool hasDirection,
        bool boost)
    {
        _snake.Step(
            fixedDeltaTime,
            requestedDirectionX,
            requestedDirectionY,
            hasDirection,
            boost);

        var snakeState = _snake.CaptureState();
        var collectedEnergy = _dots.CollectAt(snakeState.HeadPosition, _snake.Settings.HeadRadius);
        if (collectedEnergy > 0)
        {
            _snake.AddEnergy(collectedEnergy, _settings.EnergyPerSegment);
        }

        _dynamicSpawnTime += fixedDeltaTime;
        while (_dynamicSpawnTime >= _settings.DynamicSpawnIntervalSeconds)
        {
            _dynamicSpawnTime -= _settings.DynamicSpawnIntervalSeconds;
            _dots.SpawnNear(snakeState.HeadPosition);
        }

        _dots.ActivateAround(snakeState.HeadPosition);
    }

    public WorldState CaptureState()
    {
        var snake = _snake.CaptureState();
        return new WorldState(
            snake,
            _dots.ActivateAround(snake.HeadPosition),
            _dots.GeneratedCellCount,
            _dots.CollectedCount);
    }
}

public sealed record WorldState(
    SnakeState Snake,
    IReadOnlyList<DotState> ActiveDots,
    int ActiveCellCount,
    int CollectedDotCount);
