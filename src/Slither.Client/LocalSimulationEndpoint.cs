using Slither.Core;
using Slither.Protocol;

namespace Slither.Client;

public sealed class LocalSimulationEndpoint : ISimulationEndpoint
{
    private readonly WorldSimulation _simulation = new();
    private PlayerCommand _pendingCommand;

    public void Submit(in PlayerCommand command) => _pendingCommand = command;

    public void Step(double fixedDeltaTime)
    {
        _simulation.Step(
            fixedDeltaTime,
            _pendingCommand.TargetDirectionX,
            _pendingCommand.TargetDirectionY,
            _pendingCommand.HasDirection,
            _pendingCommand.Boost);
    }

    public WorldSnapshot CaptureSnapshot()
    {
        var state = _simulation.CaptureState();
        var snakeState = state.Snake;
        var body = new BodyNodeSnapshot[snakeState.Body.Count];
        for (var index = 0; index < body.Length; index++)
        {
            var node = snakeState.Body[index];
            body[index] = new BodyNodeSnapshot(
                node.Position.X,
                node.Position.Y,
                _simulation.SnakeSettings.BodyRadius);
        }

        var dots = new DotSnapshot[state.ActiveDots.Count];
        for (var index = 0; index < dots.Length; index++)
        {
            var dot = state.ActiveDots[index];
            dots[index] = new DotSnapshot(dot.Id, dot.Position.X, dot.Position.Y, dot.Radius, dot.Energy);
        }

        return new WorldSnapshot(
            snakeState.Tick,
            new ArenaSnapshot(
                0,
                0,
                _simulation.SnakeSettings.ArenaRadius,
                _simulation.SnakeSettings.ArenaRadius - _simulation.SnakeSettings.HeadRadius),
            new SnakeSnapshot(
                snakeState.HeadPosition.X,
                snakeState.HeadPosition.Y,
                snakeState.Heading.X,
                snakeState.Heading.Y,
                snakeState.TargetHeading.X,
                snakeState.TargetHeading.Y,
                snakeState.CurrentSpeed,
                _simulation.SnakeSettings.HeadRadius,
                body,
                snakeState.TargetLength,
                snakeState.IsBoosting,
                snakeState.Energy,
                SlitherSize.Calculate(
                    _simulation.SnakeSettings.InitialBodyNodes,
                    snakeState.TotalEnergy)),
            dots,
            state.ActiveCellCount,
            state.CollectedDotCount);
    }
}
