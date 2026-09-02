using Slither.Core;
using Slither.Protocol;

namespace Slither.Client;

public sealed class LocalSimulationEndpoint : ISimulationEndpoint
{
    private readonly SmokeSimulation _simulation = new();
    private PlayerCommand _pendingCommand;

    public void Submit(in PlayerCommand command) => _pendingCommand = command;

    public void Step(double fixedDeltaTime)
    {
        _simulation.Step(
            fixedDeltaTime,
            _pendingCommand.TargetDirectionX,
            _pendingCommand.TargetDirectionY,
            _pendingCommand.Boost);
    }

    public WorldSnapshot CaptureSnapshot()
    {
        var state = _simulation.CaptureState();
        return new WorldSnapshot(state.Tick, state.X, state.Y, state.Angle);
    }
}
