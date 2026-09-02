using Slither.Core;
using Slither.Protocol;

namespace Slither.Tests;

public sealed class ArchitectureBoundaryTests
{
    [Theory]
    [InlineData(typeof(SimulationSettings))]
    [InlineData(typeof(SnakeSimulation))]
    [InlineData(typeof(WorldSimulation))]
    [InlineData(typeof(PlayerCommand))]
    public void NeutralAssembliesDoNotReferencePlatformFrameworks(Type markerType)
    {
        var forbiddenReferences = markerType.Assembly
            .GetReferencedAssemblies()
            .Where(reference =>
                reference.Name?.StartsWith("MonoGame", StringComparison.OrdinalIgnoreCase) == true ||
                reference.Name?.StartsWith("Microsoft.Android", StringComparison.OrdinalIgnoreCase) == true ||
                reference.Name?.StartsWith("Xamarin.Android", StringComparison.OrdinalIgnoreCase) == true);

        Assert.Empty(forbiddenReferences);
    }
}
