using System.Collections;
using Slither.Protocol;

namespace Slither.Tests;

public sealed class ProtocolTests
{
    [Fact]
    public void CommandSequenceIsMonotonic()
    {
        var sequence = new CommandSequence();

        Assert.Equal(1u, sequence.Next());
        Assert.Equal(2u, sequence.Next());
        Assert.Equal(3u, sequence.Next());
    }

    [Fact]
    public void SnapshotDoesNotExposeMutableCollections()
    {
        var mutableProperties = typeof(WorldSnapshot)
            .GetProperties()
            .Where(property => typeof(IList).IsAssignableFrom(property.PropertyType));

        Assert.Empty(mutableProperties);
        Assert.True(typeof(WorldSnapshot).IsValueType);
    }

    [Fact]
    public void SnapshotCarriesSchemaAndInputAcknowledgement()
    {
        var snapshot = new WorldSnapshot(
            42,
            default,
            default,
            Array.Empty<DotSnapshot>(),
            0,
            0,
            LastProcessedCommandSequence: 17);

        Assert.Equal(ProtocolVersion.Current, snapshot.SchemaVersion);
        Assert.Equal(17u, snapshot.LastProcessedCommandSequence);
    }
}
