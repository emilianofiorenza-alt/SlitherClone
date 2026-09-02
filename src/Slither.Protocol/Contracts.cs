namespace Slither.Protocol;

public interface ICommandSource
{
    PlayerCommand SampleCommand();
}

public interface ISimulationEndpoint
{
    void Submit(in PlayerCommand command);

    void Step(double fixedDeltaTime);

    WorldSnapshot CaptureSnapshot();
}

public interface ISnapshotRenderer
{
    void Render(
        WorldSnapshot previousSnapshot,
        WorldSnapshot snapshot,
        float interpolationAlpha);
}
