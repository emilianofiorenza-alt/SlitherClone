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
    public void LargerSnakeTurnsMoreGraduallyAndFiltersAbruptInput()
    {
        var normal = new SnakeSimulation();
        var large = new SnakeSimulation();
        large.SetSizeScale(4.0);

        normal.Step(SimulationSettings.FixedDeltaTime, 0, 1, true, false);
        large.Step(SimulationSettings.FixedDeltaTime, 0, 1, true, false);

        var normalState = normal.CaptureState();
        var largeState = large.CaptureState();
        var normalAngle = Math.Atan2(normalState.Heading.Y, normalState.Heading.X);
        var largeAngle = Math.Atan2(largeState.Heading.Y, largeState.Heading.X);
        var largeTargetAngle = Math.Atan2(largeState.TargetHeading.Y, largeState.TargetHeading.X);

        Assert.Equal(normal.Settings.MaxTurnRateDegrees * Math.PI / 180.0 * SimulationSettings.FixedDeltaTime, normalAngle, 10);
        var expectedLargeTurnRate = large.Settings.MaxTurnRateDegrees /
                                    (1 + (large.Settings.SizeTurnPenalty * 3));
        Assert.Equal(expectedLargeTurnRate * Math.PI / 180.0 * SimulationSettings.FixedDeltaTime, largeAngle, 10);
        Assert.True(largeTargetAngle < Math.PI / 2, "The requested direction should be filtered instead of changing instantaneously.");
        Assert.True(largeAngle < normalAngle);
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
    public void LongBodyFollowsCircularTrailWithoutRapidlyCollapsingTowardHead()
    {
        var simulation = new SnakeSimulation();
        simulation.AddGrowth(35 * 5, 5);

        for (var tick = 0; tick < 2_000; tick++)
        {
            var directionAngle = tick * SimulationSettings.FixedDeltaTime * 1.4;
            simulation.Step(
                SimulationSettings.FixedDeltaTime,
                Math.Cos(directionAngle),
                Math.Sin(directionAngle),
                true,
                false);
        }

        var state = simulation.CaptureState();
        var headToTail = (state.HeadPosition - state.Body[^1].Position).Length;
        Assert.Equal(40, state.Body.Count);
        Assert.True(headToTail > 3.0, $"The circular chain collapsed to {headToTail:F3} world units.");
    }

    [Fact]
    public void CircularTrailRelaxesInwardButOnlyByAModerateAmount()
    {
        var rigid = new SnakeSimulation(SnakeSimulationSettings.Default with
        {
            BodyTrailRelaxationPerSecond = 0
        });
        var relaxed = new SnakeSimulation();
        rigid.AddGrowth(35 * 5, 5);
        relaxed.AddGrowth(35 * 5, 5);

        for (var tick = 0; tick < 2_000; tick++)
        {
            var directionAngle = tick * SimulationSettings.FixedDeltaTime * 1.4;
            rigid.Step(SimulationSettings.FixedDeltaTime, Math.Cos(directionAngle), Math.Sin(directionAngle), true, false);
            relaxed.Step(SimulationSettings.FixedDeltaTime, Math.Cos(directionAngle), Math.Sin(directionAngle), true, false);
        }

        const double turnRadiansPerSecond = 1.4;
        var relaxedState = relaxed.CaptureState();
        var center = relaxedState.HeadPosition +
                     (new WorldVector(-relaxedState.Heading.Y, relaxedState.Heading.X) *
                      (relaxedState.CurrentSpeed / turnRadiansPerSecond));
        var rigidRadius = rigid.CaptureState().Body.Average(node => (node.Position - center).Length);
        var relaxedRadius = relaxedState.Body.Average(node => (node.Position - center).Length);

        Assert.True(relaxedRadius < rigidRadius - 0.02,
            $"Relaxation was not visible: rigid={rigidRadius:F3}, relaxed={relaxedRadius:F3}.");
        Assert.True(relaxedRadius > rigidRadius - 0.60,
            $"Relaxation was too aggressive: rigid={rigidRadius:F3}, relaxed={relaxedRadius:F3}.");
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
    public void EveryTenEnergyAddsExactlyOneBodyNodeAndPreservesTotalScore()
    {
        Assert.Equal(10, WorldSimulationSettings.Default.EnergyPerSegment);
        var simulation = new SnakeSimulation();

        simulation.AddEnergy(9, 10);
        Assert.Equal(5, simulation.CaptureState().Body.Count);
        Assert.Equal(9, simulation.CaptureState().Energy);
        Assert.Equal(59, SlitherSize.Calculate(
            simulation.Settings.InitialBodyNodes,
            simulation.CaptureState().TotalEnergy));

        var tailBeforeGrowth = simulation.CaptureState().Body[^1].Position;
        simulation.AddEnergy(1, 10);
        var stateAfterGrowth = simulation.CaptureState();
        Assert.Equal(6, stateAfterGrowth.Body.Count);
        Assert.Equal(0, stateAfterGrowth.Energy);
        Assert.Equal(tailBeforeGrowth, stateAfterGrowth.Body[^1].Position);
        Assert.Equal(stateAfterGrowth.Body[^2].Position, stateAfterGrowth.Body[^1].Position);
        Assert.Equal(60, SlitherSize.Calculate(
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

        simulation.AddEnergy(20, 10);
        Assert.Equal(8, simulation.CaptureState().Body.Count);
        Assert.Equal(0, simulation.CaptureState().Energy);
        Assert.Equal(80, SlitherSize.Calculate(
            simulation.Settings.InitialBodyNodes,
            simulation.CaptureState().TotalEnergy));
    }
}
