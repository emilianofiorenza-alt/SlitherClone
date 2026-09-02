using Slither.Core;

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
    public void SimulationSelectsNormalAndBoostSpeed()
    {
        var normal = new SnakeSimulation();
        var boost = new SnakeSimulation();

        normal.Step(SimulationSettings.FixedDeltaTime, 1000, 0, true, false);
        boost.Step(SimulationSettings.FixedDeltaTime, 1000, 0, true, true);

        Assert.Equal(normal.Settings.BaseSpeed, normal.CaptureState().CurrentSpeed);
        Assert.Equal(boost.Settings.BoostSpeed, boost.CaptureState().CurrentSpeed);
        Assert.Equal(
            normal.Settings.BaseSpeed * SimulationSettings.FixedDeltaTime,
            normal.CaptureState().HeadPosition.X,
            10);
        Assert.Equal(
            boost.Settings.BoostSpeed * SimulationSettings.FixedDeltaTime,
            boost.CaptureState().HeadPosition.X,
            10);
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
}
