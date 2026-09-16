namespace Slither.Core;

public sealed class DotField
{
    private readonly WorldSimulationSettings _settings;
    private readonly double _arenaRadius;
    private readonly Dictionary<CellCoordinate, List<DotState>> _cells = [];
    private readonly HashSet<long> _collectedIds = [];
    private ulong _dynamicRandomState;
    private long _nextDynamicId = long.MinValue;

    public DotField(double arenaRadius, WorldSimulationSettings? settings = null)
    {
        _settings = settings ?? WorldSimulationSettings.Default;
        _arenaRadius = arenaRadius;
        ValidateSettings(_settings, arenaRadius);
        _dynamicRandomState = Mix((ulong)(uint)_settings.WorldSeed ^ 0xD1B54A32D192ED03UL);
    }

    public int GeneratedCellCount => _cells.Count;

    public int CollectedCount => _collectedIds.Count;

    public int ActiveDotCount => _cells.Values.Sum(static dots => dots.Count);

    public long ActiveEnergy => _cells.Values.Sum(static dots => dots.Sum(static dot => (long)dot.Energy));

    public long EnvironmentalMassGenerated { get; private set; }

    public IReadOnlyList<DotState> ActivateAround(WorldVector position)
    {
        var centerCell = GetCell(position);
        var active = new List<DotState>();
        for (var cellY = centerCell.Y - _settings.ActiveCellRadius;
             cellY <= centerCell.Y + _settings.ActiveCellRadius;
             cellY++)
        {
            for (var cellX = centerCell.X - _settings.ActiveCellRadius;
                 cellX <= centerCell.X + _settings.ActiveCellRadius;
                 cellX++)
            {
                var cell = new CellCoordinate(cellX, cellY);
                EnsureCell(cell);
                active.AddRange(_cells[cell]);
            }
        }

        return active;
    }

    public IReadOnlyList<DotState> ActivateWithinViewport(
        WorldVector position,
        double halfWidth,
        double halfHeight,
        double preloadMargin)
    {
        if (!double.IsFinite(halfWidth) || halfWidth <= 0 ||
            !double.IsFinite(halfHeight) || halfHeight <= 0 ||
            !double.IsFinite(preloadMargin) || preloadMargin < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(halfWidth));
        }

        var horizontalExtent = halfWidth + preloadMargin;
        var verticalExtent = halfHeight + preloadMargin;
        var minimumCell = GetCell(position - new WorldVector(horizontalExtent, verticalExtent));
        var maximumCell = GetCell(position + new WorldVector(horizontalExtent, verticalExtent));
        var active = new List<DotState>();
        for (var cellY = minimumCell.Y; cellY <= maximumCell.Y; cellY++)
        {
            for (var cellX = minimumCell.X; cellX <= maximumCell.X; cellX++)
            {
                var cell = new CellCoordinate(cellX, cellY);
                EnsureCell(cell);
                active.AddRange(_cells[cell]);
            }
        }

        return active;
    }

    public void EnsureAround(WorldVector position)
    {
        var centerCell = GetCell(position);
        for (var cellY = centerCell.Y - _settings.ActiveCellRadius; cellY <= centerCell.Y + _settings.ActiveCellRadius; cellY++)
        {
            for (var cellX = centerCell.X - _settings.ActiveCellRadius; cellX <= centerCell.X + _settings.ActiveCellRadius; cellX++)
            {
                EnsureCell(new CellCoordinate(cellX, cellY));
            }
        }
    }

    public bool TryFindNearest(WorldVector position, double maximumDistance, out DotState nearest)
    {
        if (!double.IsFinite(maximumDistance) || maximumDistance <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumDistance));
        }

        var centerCell = GetCell(position);
        var cellRadius = (int)Math.Ceiling(maximumDistance / _settings.DotCellSize);
        var bestDistanceSquared = maximumDistance * maximumDistance;
        nearest = default;
        var found = false;
        for (var cellY = centerCell.Y - cellRadius; cellY <= centerCell.Y + cellRadius; cellY++)
        {
            for (var cellX = centerCell.X - cellRadius; cellX <= centerCell.X + cellRadius; cellX++)
            {
                var cell = new CellCoordinate(cellX, cellY);
                EnsureCell(cell);
                foreach (var dot in _cells[cell])
                {
                    var delta = dot.Position - position;
                    var distanceSquared = (delta.X * delta.X) + (delta.Y * delta.Y);
                    if (distanceSquared >= bestDistanceSquared)
                    {
                        continue;
                    }
                    bestDistanceSquared = distanceSquared;
                    nearest = dot;
                    found = true;
                }
            }
        }
        return found;
    }

    public int CollectAt(WorldVector position, double headRadius)
        => CollectDetailedAt(position, headRadius).Score;

    public DotCollection CollectDetailedAt(
        WorldVector position,
        double headRadius,
        ulong currentTick = ulong.MaxValue)
    {
        var centerCell = GetCell(position);
        var collectedEnergy = 0;
        var collectedDots = 0;
        for (var cellY = centerCell.Y - 1; cellY <= centerCell.Y + 1; cellY++)
        {
            for (var cellX = centerCell.X - 1; cellX <= centerCell.X + 1; cellX++)
            {
                var cell = new CellCoordinate(cellX, cellY);
                EnsureCell(cell);
                var dots = _cells[cell];
                for (var index = dots.Count - 1; index >= 0; index--)
                {
                    var dot = dots[index];
                    if (dot.FadeInDurationTicks > 0 &&
                        currentTick < dot.FadeInStartTick + dot.FadeInDurationTicks)
                    {
                        continue;
                    }
                    var collisionRadius = headRadius + dot.Radius;
                    if ((dot.Position - position).Length > collisionRadius)
                    {
                        continue;
                    }

                    collectedEnergy += dot.Energy;
                    collectedDots++;
                    _collectedIds.Add(dot.Id);
                    dots.RemoveAt(index);
                }
            }
        }

        return new DotCollection(collectedDots, collectedEnergy);
    }

    public void AddDropDot(
        WorldVector position,
        int scoreValue,
        double radiusScale = 1.0,
        ulong fadeInStartTick = 0,
        ulong fadeInDurationTicks = 0,
        double? radiusOverride = null)
    {
        var energy = Math.Max(scoreValue, 1);
        if (!double.IsFinite(radiusScale) || radiusScale <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radiusScale));
        }
        var radius = radiusOverride ?? (RadiusForEnergy(energy) * radiusScale);
        if (!double.IsFinite(radius) || radius <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radiusOverride));
        }
        if (position.Length + radius >= _arenaRadius)
        {
            return;
        }

        var cell = GetCell(position);
        EnsureCell(cell);
        _cells[cell].Add(new DotState(
            _nextDynamicId++,
            position,
            radius,
            energy,
            fadeInStartTick,
            fadeInDurationTicks));
    }

    public IReadOnlyList<DotState> SpawnNear(
        WorldVector headPosition,
        int? count = null,
        ulong fadeInStartTick = 0,
        ulong fadeInDurationTicks = 0)
    {
        var spawned = new List<DotState>();
        var requestedCount = count ?? _settings.DynamicSpawnCount;
        for (var index = 0; index < requestedCount; index++)
        {
            var angle = NextDynamicDouble() * Math.PI * 2;
            var distance = _settings.DynamicSpawnMinimumRadius +
                           (NextDynamicDouble() *
                            (_settings.DynamicSpawnMaximumRadius - _settings.DynamicSpawnMinimumRadius));
            var position = headPosition + new WorldVector(Math.Cos(angle), Math.Sin(angle)) * distance;
            var energy = SelectEnergy(NextDynamicDouble());
            var radius = RadiusForEnergy(energy);
            if (position.Length + radius >= _arenaRadius)
            {
                continue;
            }

            var dot = new DotState(
                _nextDynamicId++,
                position,
                radius,
                energy,
                fadeInStartTick,
                fadeInDurationTicks);
            var cell = GetCell(position);
            EnsureCell(cell);
            _cells[cell].Add(dot);
            spawned.Add(dot);
            EnvironmentalMassGenerated += energy;
        }

        return spawned;
    }

    private void EnsureCell(CellCoordinate cell)
    {
        if (_cells.ContainsKey(cell))
        {
            return;
        }

        var dots = new List<DotState>(_settings.DotsPerCell);
        var state = CellSeed(cell);
        for (var index = 0; index < _settings.DotsPerCell; index++)
        {
            var x = (cell.X + NextDouble(ref state)) * _settings.DotCellSize;
            var y = (cell.Y + NextDouble(ref state)) * _settings.DotCellSize;
            var energy = SelectEnergy(NextDouble(ref state));
            var radius = RadiusForEnergy(energy);
            var position = new WorldVector(x, y);
            if (position.Length + radius >= _arenaRadius)
            {
                continue;
            }

            var id = StableDotId(cell, index);
            if (!_collectedIds.Contains(id))
            {
                dots.Add(new DotState(id, position, radius, energy));
            }
        }

        _cells.Add(cell, dots);
        EnvironmentalMassGenerated += dots.Sum(static dot => (long)dot.Energy);
    }

    private CellCoordinate GetCell(WorldVector position) => new(
        (int)Math.Floor(position.X / _settings.DotCellSize),
        (int)Math.Floor(position.Y / _settings.DotCellSize));

    private ulong CellSeed(CellCoordinate cell)
    {
        var seed = (ulong)(uint)_settings.WorldSeed;
        seed ^= (ulong)(uint)cell.X * 0x9E3779B185EBCA87UL;
        seed ^= (ulong)(uint)cell.Y * 0xC2B2AE3D27D4EB4FUL;
        return Mix(seed);
    }

    private long StableDotId(CellCoordinate cell, int index)
    {
        var state = CellSeed(cell) ^ ((ulong)(uint)index * 0x165667B19E3779F9UL);
        return (long)(Mix(state) & long.MaxValue);
    }

    private double NextDynamicDouble() => NextDouble(ref _dynamicRandomState);

    private int SelectEnergy(double roll)
    {
        if (roll < _settings.LargeDotProbability)
        {
            return 3;
        }

        return roll < _settings.LargeDotProbability + _settings.MediumDotProbability ? 2 : 1;
    }

    private double RadiusForEnergy(int energy) => energy switch
    {
        3 => _settings.LargeDotRadius,
        2 => _settings.MediumDotRadius,
        _ => _settings.SmallDotRadius
    };

    private static double NextDouble(ref ulong state)
    {
        state += 0x9E3779B97F4A7C15UL;
        var value = Mix(state);
        return (value >> 11) * (1.0 / (1UL << 53));
    }

    private static ulong Mix(ulong value)
    {
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }

    private static void ValidateSettings(WorldSimulationSettings settings, double arenaRadius)
    {
        if (arenaRadius <= 0 ||
            settings.DotCellSize <= 0 ||
            settings.DotsPerCell < 0 ||
            settings.ActiveCellRadius < 1 ||
            settings.SmallDotRadius <= 0 ||
            settings.MediumDotRadius < settings.SmallDotRadius ||
            settings.LargeDotRadius < settings.MediumDotRadius ||
            settings.MediumDotProbability is < 0 or > 1 ||
            settings.LargeDotProbability is < 0 or > 1 ||
            settings.MediumDotProbability + settings.LargeDotProbability > 1 ||
            settings.DynamicSpawnIntervalSeconds <= 0 ||
            settings.DynamicSpawnCount < 0 ||
            settings.DynamicSpawnMinimumRadius < 0 ||
            settings.DynamicSpawnMaximumRadius < settings.DynamicSpawnMinimumRadius ||
            settings.EnergyPerSegment < 1 ||
            settings.GrowthPerDot < 1 ||
            settings.GrowthMassPerSegment < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(settings), "World settings contain invalid values.");
        }
    }
}

public readonly record struct DotCollection(int Count, int Score);
