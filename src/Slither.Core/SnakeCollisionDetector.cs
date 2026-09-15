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
    private readonly List<CollisionBody> _bodies = [];
    private readonly HashSet<int> _candidateIndices = [];

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
        _cells.Clear();
        _bodies.Clear();
        foreach (var body in bodies)
        {
            var index = _bodies.Count;
            _bodies.Add(body);
            ForCells(body.Start, body.End, body.Radius, cell =>
            {
                if (!_cells.TryGetValue(cell, out var list))
                {
                    list = [];
                    _cells.Add(cell, list);
                }
                list.Add(index);
            });
        }
    }

    public IReadOnlyList<CollisionBody> Query(in CollisionHead head)
    {
        _candidateIndices.Clear();
        var ownerId = head.OwnerId;
        ForCells(head.Position, head.Position, head.Radius, cell =>
        {
            if (_cells.TryGetValue(cell, out var indices))
            {
                foreach (var index in indices)
                {
                    if (_bodies[index].OwnerId != ownerId)
                    {
                        _candidateIndices.Add(index);
                    }
                }
            }
        });

        CandidateCount += _candidateIndices.Count;
        var result = new List<CollisionBody>();
        foreach (var index in _candidateIndices)
        {
            NarrowPhaseCount++;
            var body = _bodies[index];
            if (CircleIntersectsCapsule(head.Position, head.Radius, body.Start, body.End, body.Radius))
            {
                result.Add(body);
            }
        }
        return result;
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

    private void ForCells(WorldVector start, WorldVector end, double radius, Action<GridCell> action)
    {
        var minX = (int)Math.Floor((Math.Min(start.X, end.X) - radius) / _cellSize);
        var maxX = (int)Math.Floor((Math.Max(start.X, end.X) + radius) / _cellSize);
        var minY = (int)Math.Floor((Math.Min(start.Y, end.Y) - radius) / _cellSize);
        var maxY = (int)Math.Floor((Math.Max(start.Y, end.Y) + radius) / _cellSize);
        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                action(new GridCell(x, y));
            }
        }
    }

    private readonly record struct GridCell(int X, int Y);
}
