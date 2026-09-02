namespace Slither.Core;

public sealed class SmokeSimulation
{
    private const float Boundary = 0.85f;
    private const float NormalSpeed = 0.45f;
    private const float BoostSpeed = 0.60f;

    private float _x = -0.55f;
    private float _y = -0.25f;
    private float _directionX = 0.82f;
    private float _directionY = 0.57f;

    public ulong Tick { get; private set; }

    public void Step(double fixedDeltaTime, float requestedX, float requestedY, bool boost)
    {
        var requestedLengthSquared = (requestedX * requestedX) + (requestedY * requestedY);
        if (requestedLengthSquared > 0.01f)
        {
            var inverseLength = 1.0f / MathF.Sqrt(requestedLengthSquared);
            _directionX = requestedX * inverseLength;
            _directionY = requestedY * inverseLength;
        }

        var speed = boost ? BoostSpeed : NormalSpeed;
        _x += _directionX * speed * (float)fixedDeltaTime;
        _y += _directionY * speed * (float)fixedDeltaTime;

        Bounce(ref _x, ref _directionX);
        Bounce(ref _y, ref _directionY);
        Tick++;
    }

    public SmokeState CaptureState() =>
        new(Tick, _x, _y, MathF.Atan2(_directionY, _directionX));

    private static void Bounce(ref float position, ref float direction)
    {
        if (position > Boundary)
        {
            position = Boundary;
            direction = -MathF.Abs(direction);
        }
        else if (position < -Boundary)
        {
            position = -Boundary;
            direction = MathF.Abs(direction);
        }
    }
}

public readonly record struct SmokeState(
    ulong Tick,
    float X,
    float Y,
    float Angle);
