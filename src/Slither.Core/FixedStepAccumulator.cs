namespace Slither.Core;

public sealed class FixedStepAccumulator
{
    private double _accumulatedTime;

    public float InterpolationAlpha =>
        (float)(_accumulatedTime / SimulationSettings.FixedDeltaTime);

    public int Advance(double elapsedSeconds, Action<double> step)
    {
        ArgumentNullException.ThrowIfNull(step);

        if (!double.IsFinite(elapsedSeconds) || elapsedSeconds < 0)
        {
            elapsedSeconds = 0;
        }

        _accumulatedTime += Math.Min(elapsedSeconds, SimulationSettings.MaximumFrameTime);

        var completedSteps = 0;
        while (_accumulatedTime >= SimulationSettings.FixedDeltaTime &&
               completedSteps < SimulationSettings.MaximumStepsPerFrame)
        {
            step(SimulationSettings.FixedDeltaTime);
            _accumulatedTime -= SimulationSettings.FixedDeltaTime;
            completedSteps++;
        }

        if (completedSteps == SimulationSettings.MaximumStepsPerFrame &&
            _accumulatedTime >= SimulationSettings.FixedDeltaTime)
        {
            _accumulatedTime %= SimulationSettings.FixedDeltaTime;
        }

        return completedSteps;
    }

    public void Reset() => _accumulatedTime = 0;
}
