using Slither.Core;

namespace Slither.Tests;

public sealed class FixedStepAccumulatorTests
{
    [Fact]
    public void TwoHalfTicksProduceOneSimulationStep()
    {
        var clock = new FixedStepAccumulator();
        var steps = 0;

        clock.Advance(SimulationSettings.FixedDeltaTime / 2, _ => steps++);
        Assert.Equal(0, steps);
        Assert.InRange(clock.InterpolationAlpha, 0.49f, 0.51f);

        clock.Advance(SimulationSettings.FixedDeltaTime / 2, _ => steps++);
        Assert.Equal(1, steps);
        Assert.InRange(clock.InterpolationAlpha, 0, 0.001f);
    }

    [Fact]
    public void LongFrameCannotCauseSpiralOfDeath()
    {
        var clock = new FixedStepAccumulator();

        var steps = clock.Advance(10, _ => { });

        Assert.Equal(SimulationSettings.MaximumStepsPerFrame, steps);
        Assert.InRange(clock.InterpolationAlpha, 0, 0.999999f);
    }

    [Fact]
    public void ResetDropsTimeAccumulatedBeforePause()
    {
        var clock = new FixedStepAccumulator();
        clock.Advance(SimulationSettings.FixedDeltaTime * 0.75, _ => { });

        clock.Reset();

        Assert.Equal(0, clock.InterpolationAlpha);
        Assert.Equal(0, clock.Advance(SimulationSettings.FixedDeltaTime * 0.5, _ => { }));
    }
}
