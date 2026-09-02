using Slither.Core;

namespace Slither.Tests;

public sealed class DotFieldTests
{
    [Fact]
    public void SameSeedProducesSameInitialDistribution()
    {
        Assert.Equal(27, WorldSimulationSettings.Default.DotsPerCell);
        Assert.Equal(4, WorldSimulationSettings.Default.DynamicSpawnCount);
        var first = new DotField(100).ActivateAround(new WorldVector(0, 0));
        var second = new DotField(100).ActivateAround(new WorldVector(0, 0));

        Assert.Equal(first, second);
    }

    [Fact]
    public void GeneratedDotsRemainInsideArena()
    {
        const double arenaRadius = 100;
        var field = new DotField(arenaRadius);
        var dots = field.ActivateAround(new WorldVector(94, 0));

        Assert.NotEmpty(dots);
        Assert.All(dots, dot => Assert.True(dot.Position.Length + dot.Radius < arenaRadius));
    }

    [Fact]
    public void CollectedDotDoesNotReappear()
    {
        var field = new DotField(100);
        var dots = field.ActivateAround(new WorldVector(0, 0));
        var target = dots[0];

        var energy = field.CollectAt(target.Position, 0.35);
        var reloaded = field.ActivateAround(target.Position);

        Assert.True(energy >= target.Energy);
        Assert.DoesNotContain(reloaded, dot => dot.Id == target.Id);
    }

    [Fact]
    public void DynamicDotsSpawnInConfiguredAreaNearSnake()
    {
        var field = new DotField(100);
        var head = new WorldVector(12, -7);

        var spawned = field.SpawnNear(head, 20);

        Assert.Equal(20, spawned.Count);
        Assert.All(spawned, dot =>
        {
            var distance = (dot.Position - head).Length;
            Assert.InRange(
                distance,
                WorldSimulationSettings.Default.DynamicSpawnMinimumRadius,
                WorldSimulationSettings.Default.DynamicSpawnMaximumRadius);
            Assert.Contains(dot.Energy, new[] { 1, 2, 3 });
        });
    }

    [Fact]
    public void EnergyClassesUseIncreasingRadii()
    {
        var settings = WorldSimulationSettings.Default;
        var field = new DotField(100, settings);
        var dots = field.SpawnNear(new WorldVector(0, 0), 500);

        Assert.Contains(dots, dot => dot.Energy == 1 && dot.Radius == settings.SmallDotRadius);
        Assert.Contains(dots, dot => dot.Energy == 2 && dot.Radius == settings.MediumDotRadius);
        Assert.Contains(dots, dot => dot.Energy == 3 && dot.Radius == settings.LargeDotRadius);
    }
}
