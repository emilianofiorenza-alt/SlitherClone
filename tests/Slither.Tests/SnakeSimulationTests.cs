using Slither.Core;
using Slither.Client;

namespace Slither.Tests;

public sealed class SnakeSimulationTests
{
    [Fact]
    public void InitializesFiveBodyNodesAtNominalSpacing()
    {
        var simulation = new SnakeSimulation();
        var state = simulation.CaptureState();
        var settings = simulation.Settings;

        Assert.Equal(5, state.Body.Count);
        var previous = state.HeadPosition;
        foreach (var node in state.Body)
        {
            Assert.Equal(settings.BodySpacing, (previous - node.Position).Length, 10);
            previous = node.Position;
        }
    }

    [Fact]
    public void TurnIsLimitedPerFixedTick()
    {
        var simulation = new SnakeSimulation();

        simulation.Step(SimulationSettings.FixedDeltaTime, 0, 1, true, false);

        var heading = simulation.CaptureState().Heading;
        var angle = Math.Atan2(heading.Y, heading.X);
        var maximumTurn = simulation.Settings.MaxTurnRateDegrees *
                          Math.PI / 180.0 *
                          SimulationSettings.FixedDeltaTime;
        Assert.Equal(maximumTurn, angle, 10);
    }

    [Fact]
    public void BoostAcceleratesAndDeceleratesProgressively()
    {
        var normal = new SnakeSimulation();
        var boost = new SnakeSimulation();

        normal.Step(SimulationSettings.FixedDeltaTime, 1000, 0, true, false);
        boost.Step(SimulationSettings.FixedDeltaTime, 1000, 0, true, true);

        Assert.Equal(normal.Settings.BaseSpeed, normal.CaptureState().CurrentSpeed);
        Assert.Equal(normal.Settings.BaseSpeed * 3, boost.Settings.BoostSpeed);
        var firstBoostSpeed = boost.Settings.BaseSpeed +
                              (boost.Settings.BoostAcceleration * SimulationSettings.FixedDeltaTime);
        Assert.Equal(firstBoostSpeed, boost.CaptureState().CurrentSpeed, 10);
        Assert.Equal(
            normal.Settings.BaseSpeed * SimulationSettings.FixedDeltaTime,
            normal.CaptureState().HeadPosition.X,
            10);
        Assert.Equal(
            firstBoostSpeed * SimulationSettings.FixedDeltaTime,
            boost.CaptureState().HeadPosition.X,
            10);

        for (var tick = 0; tick < 30; tick++)
        {
            boost.Step(SimulationSettings.FixedDeltaTime, 1, 0, true, true);
        }
        Assert.Equal(boost.Settings.BoostSpeed, boost.CaptureState().CurrentSpeed);

        boost.Step(SimulationSettings.FixedDeltaTime, 1, 0, true, false);
        Assert.Equal(
            boost.Settings.BoostSpeed -
            (boost.Settings.BoostDeceleration * SimulationSettings.FixedDeltaTime),
            boost.CaptureState().CurrentSpeed,
            10);

        for (var tick = 0; tick < 30; tick++)
        {
            boost.Step(SimulationSettings.FixedDeltaTime, 1, 0, true, false);
        }
        Assert.Equal(boost.Settings.BaseSpeed, boost.CaptureState().CurrentSpeed);
    }

    [Fact]
    public void BodyChainStaysFiniteAndConnectedDuringLongCircularTurn()
    {
        var simulation = new SnakeSimulation();
        const int ticks = 6000;

        for (var tick = 0; tick < ticks; tick++)
        {
            var targetAngle = tick * SimulationSettings.FixedDeltaTime * 1.4;
            simulation.Step(
                SimulationSettings.FixedDeltaTime,
                Math.Cos(targetAngle),
                Math.Sin(targetAngle),
                true,
                tick % 180 < 60);

            var state = simulation.CaptureState();
            var previous = state.HeadPosition;
            foreach (var node in state.Body)
            {
                var distance = (previous - node.Position).Length;
                Assert.True(double.IsFinite(distance));
                Assert.True(distance <= simulation.Settings.BodySpacing + 1e-9);
                previous = node.Position;
            }
        }

        Assert.Equal((ulong)ticks, simulation.Tick);
    }

    [Fact]
    public void HeadCannotCrossArenaBoundaryAndTurnsBackInward()
    {
        var settings = SnakeSimulationSettings.Default with
        {
            ArenaRadius = 4,
            InitialBodyNodes = 2
        };
        var simulation = new SnakeSimulation(settings);
        var observedInwardHeading = false;

        for (var tick = 0; tick < 240; tick++)
        {
            simulation.Step(SimulationSettings.FixedDeltaTime, 1, 0, true, false);
            var state = simulation.CaptureState();
            Assert.True(state.HeadPosition.Length <= settings.ArenaRadius - settings.HeadRadius + 1e-9);
            observedInwardHeading |= state.HeadPosition.X > 0 && state.Heading.X < 0;
        }

        Assert.True(observedInwardHeading);
    }

    [Fact]
    public void EveryFiveEnergyAddsExactlyOneBodyNodeAndPreservesTotalScore()
    {
        Assert.Equal(5, WorldSimulationSettings.Default.EnergyPerSegment);
        var simulation = new SnakeSimulation();

        simulation.AddEnergy(4, 5);
        Assert.Equal(5, simulation.CaptureState().Body.Count);
        Assert.Equal(4, simulation.CaptureState().Energy);
        Assert.Equal(54, SlitherSize.Calculate(
            simulation.Settings.InitialBodyNodes,
            simulation.CaptureState().TotalEnergy));

        var tailBeforeGrowth = simulation.CaptureState().Body[^1].Position;
        simulation.AddEnergy(1, 5);
        var stateAfterGrowth = simulation.CaptureState();
        Assert.Equal(6, stateAfterGrowth.Body.Count);
        Assert.Equal(0, stateAfterGrowth.Energy);
        Assert.Equal(tailBeforeGrowth, stateAfterGrowth.Body[^1].Position);
        Assert.Equal(stateAfterGrowth.Body[^2].Position, stateAfterGrowth.Body[^1].Position);
        Assert.Equal(55, SlitherSize.Calculate(
            simulation.Settings.InitialBodyNodes,
            stateAfterGrowth.TotalEnergy));

        for (var tick = 0; tick < 30; tick++)
        {
            simulation.Step(SimulationSettings.FixedDeltaTime, 1, 0, true, false);
        }

        var movedState = simulation.CaptureState();
        var newTailSpacing = (movedState.Body[^2].Position - movedState.Body[^1].Position).Length;
        Assert.True(newTailSpacing > 0);
        Assert.True(newTailSpacing <= simulation.Settings.BodySpacing + 1e-10);

        simulation.AddEnergy(10, 5);
        Assert.Equal(8, simulation.CaptureState().Body.Count);
        Assert.Equal(0, simulation.CaptureState().Energy);
        Assert.Equal(65, SlitherSize.Calculate(
            simulation.Settings.InitialBodyNodes,
            simulation.CaptureState().TotalEnergy));
    }
}
