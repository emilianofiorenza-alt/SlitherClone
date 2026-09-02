namespace Slither.Client;

public readonly record struct ScreenPoint(float X, float Y)
{
    public static ScreenPoint operator +(ScreenPoint point, ScreenPoint offset) =>
        new(point.X + offset.X, point.Y + offset.Y);

    public static ScreenPoint operator -(ScreenPoint left, ScreenPoint right) =>
        new(left.X - right.X, left.Y - right.Y);
}

public readonly record struct ScreenInsets(float Left, float Top, float Right, float Bottom)
{
    public static ScreenInsets None => new(0, 0, 0, 0);
}

public readonly record struct CircleRegion(ScreenPoint Center, float Radius)
{
    public bool Contains(ScreenPoint point)
    {
        var delta = point - Center;
        return (delta.X * delta.X) + (delta.Y * delta.Y) <= Radius * Radius;
    }
}

public readonly record struct PointerSample(int Id, ScreenPoint Position);
