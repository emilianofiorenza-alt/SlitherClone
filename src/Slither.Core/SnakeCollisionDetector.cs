namespace Slither.Core;

public readonly record struct CollisionBody(
    SnakeId OwnerId,
    int Generation,
    WorldVector Start,
    WorldVector End,
    double Radius,
    int SegmentIndex);

public readonly record struct CollisionHead(
    SnakeId OwnerId,
    int Generation,
    WorldVector Position,
    double Radius);

public sealed class SnakeCollisionDetector
{
    private readonly double _cellSize;
    private readonly Dictionary<GridCell, List<int>> _cells = [];
    private readonly List<GridCell> _activeCells = [];
    private readonly List<CollisionBody> _bodies = [];
    private readonly HashSet<int> _candidateIndices = [];
    private readonly List<CollisionBody> _hits = [];

    public SnakeCollisionDetector(double cellSize)
    {
        if (cellSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cellSize));
        }

        _cellSize = cellSize;
    }

    public int CandidateCount { get; private set; }

    public int NarrowPhaseCount { get; private set; }

    public void Rebuild(IEnumerable<CollisionBody> bodies)
    {
        foreach (var cell in _activeCells) _cells[cell].Clear();
        _activeCells.Clear();
        _bodies.Clear();
        foreach (var body in bodies)
        {
            var index = _bodies.Count;
            _bodies.Add(body);
            var minX = CellCoordinate(Math.Min(body.Start.X, body.End.X) - body.Radius);
            var maxX = CellCoordinate(Math.Max(body.Start.X, body.End.X) + body.Radius);
            var minY = CellCoordinate(Math.Min(body.Start.Y, body.End.Y) - body.Radius);
            var maxY = CellCoordinate(Math.Max(body.Start.Y, body.End.Y) + body.Radius);
            for (var y = minY; y <= maxY; y++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    var cell = new GridCell(x, y);
                    if (!_cells.TryGetValue(cell, out var list))
                    {
                        list = [];
                        _cells.Add(cell, list);
                    }
                    if (list.Count == 0) _activeCells.Add(cell);
                    list.Add(index);
                }
            }
        }
    }

    public IReadOnlyList<CollisionBody> Query(in CollisionHead head)
    {
        _candidateIndices.Clear();
        _hits.Clear();
        var ownerId = head.OwnerId;
        var minX = CellCoordinate(head.Position.X - head.Radius);
        var maxX = CellCoordinate(head.Position.X + head.Radius);
        var minY = CellCoordinate(head.Position.Y - head.Radius);
        var maxY = CellCoordinate(head.Position.Y + head.Radius);
        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                if (!_cells.TryGetValue(new GridCell(x, y), out var indices)) continue;
                foreach (var index in indices)
                {
                    if (_bodies[index].OwnerId != ownerId) _candidateIndices.Add(index);
                }
            }
        }

        CandidateCount += _candidateIndices.Count;
        foreach (var index in _candidateIndices)
        {
            NarrowPhaseCount++;
            var body = _bodies[index];
            if (CircleIntersectsCapsule(head.Position, head.Radius, body.Start, body.End, body.Radius))
            {
                _hits.Add(body);
            }
        }
        return _hits;
    }

    public void BeginTick()
    {
        CandidateCount = 0;
        NarrowPhaseCount = 0;
    }

    public static bool CircleIntersectsCapsule(
        WorldVector center,
        double circleRadius,
        WorldVector start,
        WorldVector end,
        double capsuleRadius)
    {
        var segment = end - start;
        var lengthSquared = (segment.X * segment.X) + (segment.Y * segment.Y);
        var t = lengthSquared <= double.Epsilon
            ? 0
            : (((center.X - start.X) * segment.X) + ((center.Y - start.Y) * segment.Y)) / lengthSquared;
        t = Math.Clamp(t, 0, 1);
        var closest = start + (segment * t);
        var delta = center - closest;
        var radius = circleRadius + capsuleRadius;
        return (delta.X * delta.X) + (delta.Y * delta.Y) <= radius * radius;
    }

    public static IReadOnlyList<CollisionBody> QueryBruteForce(
        in CollisionHead head,
        IEnumerable<CollisionBody> bodies)
    {
        var result = new List<CollisionBody>();
        foreach (var body in bodies)
        {
            if (body.OwnerId != head.OwnerId &&
                CircleIntersectsCapsule(head.Position, head.Radius, body.Start, body.End, body.Radius))
            {
                result.Add(body);
            }
        }
        return result;
    }

    private int CellCoordinate(double value) => (int)Math.Floor(value / _cellSize);

    private readonly record struct GridCell(int X, int Y);
}
