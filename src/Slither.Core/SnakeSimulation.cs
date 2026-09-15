namespace Slither.Core;

public sealed class SnakeSimulation
{
    private readonly SnakeSimulationSettings _settings;
    private readonly List<WorldVector> _body;
    private readonly List<double> _bodyFollowDistances;
    private readonly List<WorldVector> _trail;
    private int _trailStartIndex;
    private double _trailLength;
    private WorldVector _headPosition = new(0, 0);
    private WorldVector _heading = new(1, 0);
    private WorldVector _targetHeading = new(1, 0);
    private double _currentSpeed;
    private bool _isBoosting;
    private bool _isBoundaryCorrecting;
    private int _energy;
    private int _totalEnergy;
    private double _sizeScale = 1.0;
    private int _targetBodyNodeCount;

    public SnakeSimulation(
        SnakeSimulationSettings? settings = null,
        WorldVector? initialPosition = null,
        WorldVector? initialHeading = null)
    {
        _settings = settings ?? SnakeSimulationSettings.Default;
        ValidateSettings(_settings);
        _headPosition = initialPosition ?? new WorldVector(0, 0);
        _heading = (initialHeading ?? new WorldVector(1, 0)).NormalizedOr(new WorldVector(1, 0));
        _targetHeading = _heading;
        _currentSpeed = _settings.BaseSpeed;
        _body = new List<WorldVector>(_settings.InitialBodyNodes);
        _bodyFollowDistances = new List<double>(_settings.InitialBodyNodes);
        _trail = new List<WorldVector>(_settings.InitialBodyNodes + 128);

        for (var index = 0; index < _settings.InitialBodyNodes; index++)
        {
            _body.Add(_headPosition - (_heading * (_settings.BodySpacing * (index + 1))));
            _bodyFollowDistances.Add(_settings.BodySpacing * (index + 1));
        }

        for (var index = _body.Count - 1; index >= 0; index--)
        {
            _trail.Add(_body[index]);
        }
        _trail.Add(_headPosition);
        _trailLength = _settings.BodySpacing * _settings.InitialBodyNodes;
        _targetBodyNodeCount = _body.Count;
    }

    public ulong Tick { get; private set; }

    public SnakeSimulationSettings Settings => _settings;

    public WorldVector HeadPosition => _headPosition;

    public WorldVector Heading => _heading;

    public IReadOnlyList<WorldVector> BodyPositions => _body;

    public double SizeScale => _sizeScale;

    public double HeadRadius => _settings.HeadRadius * _sizeScale;

    public double BodyRadius => _settings.BodyRadius * _sizeScale;

    public double BodySpacing => _settings.BodySpacing * _sizeScale;

    public void SetSizeScale(double sizeScale)
    {
        if (!double.IsFinite(sizeScale) || sizeScale <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeScale));
        }
        _sizeScale = sizeScale;
    }

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
            _targetBodyNodeCount = _body.Count;
        }
    }

    public void AddGrowth(int growthMass, int growthMassPerSegment) =>
        AddEnergy(growthMass, growthMassPerSegment);

    public void EnsureBodyNodeCount(int targetCount)
    {
        if (targetCount < 1) throw new ArgumentOutOfRangeException(nameof(targetCount));
        while (_body.Count < targetCount) AppendBodyNode();
        _targetBodyNodeCount = targetCount;
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
            var requestedHeading = new WorldVector(requestedDirectionX, requestedDirectionY)
                .NormalizedOr(_targetHeading);
            _targetHeading = RotateTowards(
                _targetHeading,
                requestedHeading,
                ScaledTurnRate(_settings.InputFilterTurnRateDegrees),
                fixedDeltaTime);
        }

        _isBoosting = boost;
        var targetSpeed = boost ? _settings.BoostSpeed : _settings.BaseSpeed;
        var acceleration = boost ? _settings.BoostAcceleration : _settings.BoostDeceleration;
        _currentSpeed = MoveTowards(
            _currentSpeed,
            targetSpeed,
            acceleration * fixedDeltaTime);
        TurnTowardsTarget(fixedDeltaTime);
        var previousHeadPosition = _headPosition;
        _headPosition += _heading * (_currentSpeed * fixedDeltaTime);
        ConstrainToArena(fixedDeltaTime);
        UpdateTrailAndBody((previousHeadPosition - _headPosition).Length, fixedDeltaTime);
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
            BodySpacing * _targetBodyNodeCount,
            _isBoosting,
            _energy,
            _totalEnergy,
            _sizeScale);
    }

    private void TurnTowardsTarget(double fixedDeltaTime)
    {
        var cross = (_heading.X * _targetHeading.Y) - (_heading.Y * _targetHeading.X);
        var dot = (_heading.X * _targetHeading.X) + (_heading.Y * _targetHeading.Y);
        var angularError = Math.Atan2(cross, dot);
        var turnRateDegrees = _isBoosting
            ? _settings.BoostMaxTurnRateDegrees
            : _settings.MaxTurnRateDegrees;
        turnRateDegrees = ScaledTurnRate(turnRateDegrees);
        var maximumTurn = turnRateDegrees * (Math.PI / 180.0) * fixedDeltaTime;
        var appliedTurn = Math.Clamp(angularError, -maximumTurn, maximumTurn);
        var cosine = Math.Cos(appliedTurn);
        var sine = Math.Sin(appliedTurn);
        _heading = new WorldVector(
            (_heading.X * cosine) - (_heading.Y * sine),
            (_heading.X * sine) + (_heading.Y * cosine)).NormalizedOr(_heading);
    }

    private double ScaledTurnRate(double turnRateDegrees) =>
        turnRateDegrees /
        (1.0 + (_settings.SizeTurnPenalty * Math.Max(0.0, _sizeScale - 1.0)));

    private static WorldVector RotateTowards(
        WorldVector current,
        WorldVector target,
        double turnRateDegrees,
        double fixedDeltaTime)
    {
        var cross = (current.X * target.Y) - (current.Y * target.X);
        var dot = (current.X * target.X) + (current.Y * target.Y);
        var angularError = Math.Atan2(cross, dot);
        var maximumTurn = turnRateDegrees * (Math.PI / 180.0) * fixedDeltaTime;
        var appliedTurn = Math.Clamp(angularError, -maximumTurn, maximumTurn);
        var cosine = Math.Cos(appliedTurn);
        var sine = Math.Sin(appliedTurn);
        return new WorldVector(
            (current.X * cosine) - (current.Y * sine),
            (current.X * sine) + (current.Y * cosine)).NormalizedOr(current);
    }

    private void UpdateTrailAndBody(double distanceTravelled, double fixedDeltaTime)
    {
        if (distanceTravelled > 1e-12)
        {
            _trail.Add(_headPosition);
            _trailLength += distanceTravelled;
            for (var index = 0; index < _bodyFollowDistances.Count; index++)
            {
                var nominalDistance = BodySpacing * Math.Min(index + 1, _targetBodyNodeCount);
                _bodyFollowDistances[index] = MoveTowards(
                    _bodyFollowDistances[index], nominalDistance, distanceTravelled);
            }

            // Retire surplus nodes only once they overlap the preceding node.
            // All distances change through movement, never by cutting off the tail.
            while (_body.Count > _targetBodyNodeCount &&
                   Math.Abs(_bodyFollowDistances[^1] - _bodyFollowDistances[^2]) <= 1e-10)
            {
                _body.RemoveAt(_body.Count - 1);
                _bodyFollowDistances.RemoveAt(_bodyFollowDistances.Count - 1);
            }
        }

        RelaxTrail(fixedDeltaTime);
        SampleBodyFromTrail();
        PruneTrail();
    }

    private void RelaxTrail(double fixedDeltaTime)
    {
        var span = _settings.BodyTrailRelaxationSpan;
        if (_trail.Count - _trailStartIndex < (span * 2) + 1)
        {
            return;
        }

        var amount = 1.0 - Math.Exp(-_settings.BodyTrailRelaxationPerSecond * fixedDeltaTime);
        for (var index = _trailStartIndex + span; index < _trail.Count - span; index++)
        {
            var midpoint = (_trail[index - span] + _trail[index + span]) * 0.5;
            _trail[index] += (midpoint - _trail[index]) * amount;
        }

        _trailLength = 0;
        for (var index = _trailStartIndex + 1; index < _trail.Count; index++)
        {
            _trailLength += (_trail[index] - _trail[index - 1]).Length;
        }
    }

    private void SampleBodyFromTrail()
    {
        var trailIndex = _trail.Count - 1;
        var distanceAtNewerPoint = 0.0;
        var newerPoint = _trail[trailIndex];
        for (var bodyIndex = 0; bodyIndex < _body.Count; bodyIndex++)
        {
            var targetDistance = _bodyFollowDistances[bodyIndex];
            while (trailIndex > _trailStartIndex)
            {
                var olderPoint = _trail[trailIndex - 1];
                var segmentLength = (newerPoint - olderPoint).Length;
                if (distanceAtNewerPoint + segmentLength >= targetDistance)
                {
                    var amount = segmentLength <= 1e-12
                        ? 0
                        : (targetDistance - distanceAtNewerPoint) / segmentLength;
                    _body[bodyIndex] = newerPoint + ((olderPoint - newerPoint) * amount);
                    break;
                }
                distanceAtNewerPoint += segmentLength;
                trailIndex--;
                newerPoint = olderPoint;
            }

            if (trailIndex == _trailStartIndex && distanceAtNewerPoint < targetDistance)
            {
                _body[bodyIndex] = _trail[_trailStartIndex];
            }
        }
    }

    private void PruneTrail()
    {
        var requiredLength = _bodyFollowDistances[^1] + BodySpacing;
        while (_trailStartIndex + 1 < _trail.Count)
        {
            var firstSegmentLength = (_trail[_trailStartIndex + 1] - _trail[_trailStartIndex]).Length;
            if (_trailLength - firstSegmentLength < requiredLength)
            {
                break;
            }
            _trailLength -= firstSegmentLength;
            _trailStartIndex++;
        }

        if (_trailStartIndex < 1024)
        {
            return;
        }
        _trail.RemoveRange(0, _trailStartIndex);
        _trailStartIndex = 0;
    }

    private void AppendBodyNode()
    {
        var tail = _body[^1];
        _body.Add(tail);
        _bodyFollowDistances.Add(_bodyFollowDistances[^1]);
    }

    private void ConstrainToArena(double fixedDeltaTime)
    {
        var distanceFromCenter = _headPosition.Length;
        var playableRadius = _settings.ArenaRadius - HeadRadius;
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
        if (inwardAlignment > 0.35 && _headPosition.Length < playableRadius - HeadRadius)
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
            settings.InputFilterTurnRateDegrees <= 0 ||
            settings.SizeTurnPenalty < 0 ||
            settings.HeadRadius <= 0 ||
            settings.BodyRadius <= 0 ||
            settings.BodySpacing <= 0 ||
            settings.InitialBodyNodes < 1 ||
            settings.ConstraintIterations < 1 ||
            settings.BodyTrailRelaxationPerSecond < 0 ||
            settings.BodyTrailRelaxationSpan < 1 ||
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
