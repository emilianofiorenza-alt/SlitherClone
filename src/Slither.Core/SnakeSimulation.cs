namespace Slither.Core;

public sealed class SnakeSimulation
{
    private readonly SnakeSimulationSettings _settings;
    private readonly List<WorldVector> _body;
    private WorldVector _headPosition = new(0, 0);
    private WorldVector _heading = new(1, 0);
    private WorldVector _targetHeading = new(1, 0);
    private double _currentSpeed;
    private bool _isBoosting;
    private bool _isBoundaryCorrecting;
    private int _energy;
    private int _totalEnergy;

    public SnakeSimulation(SnakeSimulationSettings? settings = null)
    {
        _settings = settings ?? SnakeSimulationSettings.Default;
        ValidateSettings(_settings);
        _currentSpeed = _settings.BaseSpeed;
        _body = new List<WorldVector>(_settings.InitialBodyNodes);

        for (var index = 0; index < _settings.InitialBodyNodes; index++)
        {
            _body.Add(_headPosition - (_heading * (_settings.BodySpacing * (index + 1))));
        }
    }

    public ulong Tick { get; private set; }

    public SnakeSimulationSettings Settings => _settings;

    public void AddEnergy(int energy, int energyPerSegment)
    {
        if (energy < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(energy));
        }

        if (energyPerSegment < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(energyPerSegment));
        }

        _energy += energy;
        _totalEnergy += energy;
        while (_energy >= energyPerSegment)
        {
            _energy -= energyPerSegment;
            AppendBodyNode();
        }
    }

    public void Step(
        double fixedDeltaTime,
        double requestedDirectionX,
        double requestedDirectionY,
        bool hasDirection,
        bool boost)
    {
        if (!double.IsFinite(fixedDeltaTime) || fixedDeltaTime <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fixedDeltaTime));
        }

        if (hasDirection && !_isBoundaryCorrecting)
        {
            _targetHeading = new WorldVector(requestedDirectionX, requestedDirectionY)
                .NormalizedOr(_targetHeading);
        }

        _isBoosting = boost;
        var targetSpeed = boost ? _settings.BoostSpeed : _settings.BaseSpeed;
        var acceleration = boost ? _settings.BoostAcceleration : _settings.BoostDeceleration;
        _currentSpeed = MoveTowards(
            _currentSpeed,
            targetSpeed,
            acceleration * fixedDeltaTime);
        TurnTowardsTarget(fixedDeltaTime);
        _headPosition += _heading * (_currentSpeed * fixedDeltaTime);
        ConstrainToArena(fixedDeltaTime);
        UpdateBodyChain();
        Tick++;
    }

    public SnakeState CaptureState()
    {
        var bodySnapshot = new BodyNode[_body.Count];
        for (var index = 0; index < _body.Count; index++)
        {
            bodySnapshot[index] = new BodyNode(_body[index]);
        }

        return new SnakeState(
            Tick,
            _headPosition,
            _heading,
            _targetHeading,
            _currentSpeed,
            bodySnapshot,
            _settings.BodySpacing * _body.Count,
            _isBoosting,
            _energy,
            _totalEnergy);
    }

    private void TurnTowardsTarget(double fixedDeltaTime)
    {
        var cross = (_heading.X * _targetHeading.Y) - (_heading.Y * _targetHeading.X);
        var dot = (_heading.X * _targetHeading.X) + (_heading.Y * _targetHeading.Y);
        var angularError = Math.Atan2(cross, dot);
        var turnRateDegrees = _isBoosting
            ? _settings.BoostMaxTurnRateDegrees
            : _settings.MaxTurnRateDegrees;
        var maximumTurn = turnRateDegrees * (Math.PI / 180.0) * fixedDeltaTime;
        var appliedTurn = Math.Clamp(angularError, -maximumTurn, maximumTurn);
        var cosine = Math.Cos(appliedTurn);
        var sine = Math.Sin(appliedTurn);
        _heading = new WorldVector(
            (_heading.X * cosine) - (_heading.Y * sine),
            (_heading.X * sine) + (_heading.Y * cosine)).NormalizedOr(_heading);
    }

    private void UpdateBodyChain()
    {
        for (var iteration = 0; iteration < _settings.ConstraintIterations; iteration++)
        {
            var previous = _headPosition;
            for (var index = 0; index < _body.Count; index++)
            {
                var delta = previous - _body[index];
                var distance = delta.Length;
                if (distance > _settings.BodySpacing)
                {
                    _body[index] += delta * ((distance - _settings.BodySpacing) / distance);
                }

                previous = _body[index];
            }
        }
    }

    private void AppendBodyNode()
    {
        var tail = _body[^1];
        _body.Add(tail);
    }

    private void ConstrainToArena(double fixedDeltaTime)
    {
        var distanceFromCenter = _headPosition.Length;
        var playableRadius = _settings.ArenaRadius - _settings.HeadRadius;
        if (distanceFromCenter > playableRadius)
        {
            var outward = _headPosition.NormalizedOr(new WorldVector(1, 0));
            _headPosition = outward * playableRadius;
            _targetHeading = outward * -1;
            _isBoundaryCorrecting = true;
        }

        if (!_isBoundaryCorrecting)
        {
            return;
        }

        var inward = _headPosition.NormalizedOr(new WorldVector(1, 0)) * -1;
        _targetHeading = inward;
        TurnTowards(_targetHeading, _settings.BoundaryTurnRateDegrees, fixedDeltaTime);

        var inwardAlignment = (_heading.X * inward.X) + (_heading.Y * inward.Y);
        if (inwardAlignment > 0.35 && _headPosition.Length < playableRadius - _settings.HeadRadius)
        {
            _isBoundaryCorrecting = false;
        }
    }

    private void TurnTowards(WorldVector target, double turnRateDegrees, double fixedDeltaTime)
    {
        var cross = (_heading.X * target.Y) - (_heading.Y * target.X);
        var dot = (_heading.X * target.X) + (_heading.Y * target.Y);
        var angularError = Math.Atan2(cross, dot);
        var maximumTurn = turnRateDegrees * (Math.PI / 180.0) * fixedDeltaTime;
        var appliedTurn = Math.Clamp(angularError, -maximumTurn, maximumTurn);
        var cosine = Math.Cos(appliedTurn);
        var sine = Math.Sin(appliedTurn);
        _heading = new WorldVector(
            (_heading.X * cosine) - (_heading.Y * sine),
            (_heading.X * sine) + (_heading.Y * cosine)).NormalizedOr(_heading);
    }

    private static void ValidateSettings(SnakeSimulationSettings settings)
    {
        if (settings.BaseSpeed <= 0 ||
            settings.BoostSpeed < settings.BaseSpeed ||
            settings.BoostAcceleration <= 0 ||
            settings.BoostDeceleration <= 0 ||
            settings.MaxTurnRateDegrees <= 0 ||
            settings.BoostMaxTurnRateDegrees <= 0 ||
            settings.HeadRadius <= 0 ||
            settings.BodyRadius <= 0 ||
            settings.BodySpacing <= 0 ||
            settings.InitialBodyNodes < 1 ||
            settings.ConstraintIterations < 1 ||
            settings.ArenaRadius <= settings.HeadRadius ||
            settings.BoundaryTurnRateDegrees <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(settings), "Snake settings contain invalid values.");
        }
    }

    private static double MoveTowards(double current, double target, double maximumDelta)
    {
        if (Math.Abs(target - current) <= maximumDelta)
        {
            return target;
        }

        return current + (Math.Sign(target - current) * maximumDelta);
    }
}
