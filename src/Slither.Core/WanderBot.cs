namespace Slither.Core;

public sealed class WanderBot
{
    private readonly WorldSimulationSettings _settings;
    private ulong _randomState;
    private WorldVector _desiredDirection;
    private WorldVector _filteredDirection;
    private double _directionTime;
    private double _boostTime;

    public WanderBot(
        int worldSeed,
        SnakeId snakeId,
        int generation,
        WorldVector initialHeading,
        WorldSimulationSettings settings)
    {
        _settings = settings;
        _desiredDirection = initialHeading.NormalizedOr(new WorldVector(1, 0));
        _filteredDirection = _desiredDirection;
        _randomState = Mix((ulong)(uint)worldSeed ^
                           ((ulong)(uint)snakeId.Value * 0x9E3779B185EBCA87UL) ^
                           ((ulong)(uint)generation * 0xC2B2AE3D27D4EB4FUL));
        SelectDirection(_desiredDirection);
    }

    public SnakeControl Sample(
        double fixedDeltaTime,
        WorldVector headPosition,
        WorldVector heading,
        double arenaRadius,
        WorldVector? dotTarget = null,
        WorldVector avoidance = default)
    {
        _directionTime -= fixedDeltaTime;
        _boostTime = Math.Max(0, _boostTime - fixedDeltaTime);

        var distance = headPosition.Length;
        if (distance >= arenaRadius - _settings.BotBoundaryMargin)
        {
            _desiredDirection = (headPosition * -1).NormalizedOr(heading);
            _directionTime = Math.Max(_directionTime, 0.25);
        }
        else if (_directionTime <= 0)
        {
            SelectDirection(heading);
        }

        if (_boostTime <= 0 && NextDouble() < _settings.BotBoostStartsPerSecond * fixedDeltaTime)
        {
            _boostTime = Lerp(
                _settings.BotBoostMinimumSeconds,
                _settings.BotBoostMaximumSeconds,
                NextDouble());
        }

        var steering = _desiredDirection;
        if (distance < arenaRadius - _settings.BotBoundaryMargin)
        {
            if (dotTarget is { } target)
            {
                var attraction = (target - headPosition).NormalizedOr(steering);
                steering += attraction * _settings.BotDotAttractionWeight;
            }
            steering += avoidance * _settings.BotOpponentAvoidanceWeight;
        }
        steering = steering.NormalizedOr(_desiredDirection);
        _filteredDirection = RotateTowards(
            _filteredDirection,
            steering,
            _settings.BotInputTurnRateDegrees * (Math.PI / 180.0) * fixedDeltaTime);
        return new SnakeControl(_filteredDirection.X, _filteredDirection.Y, true, _boostTime > 0);
    }

    private void SelectDirection(WorldVector currentHeading)
    {
        var maximumRadians = _settings.BotMaximumDeviationDegrees * Math.PI / 180.0;
        var angle = ((NextDouble() * 2) - 1) * maximumRadians;
        var cosine = Math.Cos(angle);
        var sine = Math.Sin(angle);
        _desiredDirection = new WorldVector(
            (currentHeading.X * cosine) - (currentHeading.Y * sine),
            (currentHeading.X * sine) + (currentHeading.Y * cosine)).NormalizedOr(currentHeading);
        _directionTime = Lerp(
            _settings.BotDirectionMinimumSeconds,
            _settings.BotDirectionMaximumSeconds,
            NextDouble());
    }

    private double NextDouble()
    {
        _randomState += 0x9E3779B97F4A7C15UL;
        var value = Mix(_randomState);
        return (value >> 11) * (1.0 / (1UL << 53));
    }

    private static double Lerp(double start, double end, double amount) =>
        start + ((end - start) * amount);

    private static WorldVector RotateTowards(WorldVector current, WorldVector target, double maximumRadians)
    {
        current = current.NormalizedOr(new WorldVector(1, 0));
        target = target.NormalizedOr(current);
        var cross = (current.X * target.Y) - (current.Y * target.X);
        var dot = Math.Clamp((current.X * target.X) + (current.Y * target.Y), -1, 1);
        var angle = Math.Atan2(cross, dot);
        var applied = Math.Clamp(angle, -maximumRadians, maximumRadians);
        var cosine = Math.Cos(applied);
        var sine = Math.Sin(applied);
        return new WorldVector(
            (current.X * cosine) - (current.Y * sine),
            (current.X * sine) + (current.Y * cosine)).NormalizedOr(current);
    }

    private static ulong Mix(ulong value)
    {
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }
}
