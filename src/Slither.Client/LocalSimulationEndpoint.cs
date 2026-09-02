using Slither.Core;
using Slither.Protocol;

namespace Slither.Client;

public sealed class LocalSimulationEndpoint : ISimulationEndpoint
{
    private readonly SnakeSimulation _simulation = new();
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
        var body = new BodyNodeSnapshot[state.Body.Count];
        for (var index = 0; index < body.Length; index++)
        {
            var node = state.Body[index];
            body[index] = new BodyNodeSnapshot(
                node.Position.X,
                node.Position.Y,
                _simulation.Settings.BodyRadius);
        }

        return new WorldSnapshot(
            state.Tick,
            new ArenaSnapshot(
                0,
                0,
                _simulation.Settings.ArenaRadius,
                _simulation.Settings.ArenaRadius - _simulation.Settings.HeadRadius),
            new SnakeSnapshot(
                state.HeadPosition.X,
                state.HeadPosition.Y,
                state.Heading.X,
                state.Heading.Y,
                state.TargetHeading.X,
                state.TargetHeading.Y,
                state.CurrentSpeed,
                _simulation.Settings.HeadRadius,
                body,
                state.TargetLength,
                state.IsBoosting));
    }
}
