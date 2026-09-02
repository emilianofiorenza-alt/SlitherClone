namespace Slither.Core;

public static class SimulationSettings
{
    public const int TicksPerSecond = 60;
    public const double FixedDeltaTime = 1.0 / TicksPerSecond;
    public const double MaximumFrameTime = 0.25;
    public const int MaximumStepsPerFrame = 8;
}
