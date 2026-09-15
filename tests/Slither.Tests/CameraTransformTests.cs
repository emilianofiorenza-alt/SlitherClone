using Slither.Client;

namespace Slither.Tests;

public sealed class CameraTransformTests
{
    [Fact]
    public void CameraPositionMapsToSafeGameplayCenter()
    {
        var layout = ScreenLayout.Create(1200, 800, new ScreenInsets(40, 20, 80, 60));
        var camera = new CameraTransform();

        var screen = camera.WorldToScreen(layout, 25, -12, 25, -12);

        Assert.Equal(layout.GameplayCenter.X, screen.X, 3);
        Assert.Equal(layout.GameplayCenter.Y, screen.Y, 3);
    }

    [Fact]
    public void WorldOffsetUsesConstantScaleAndUpwardWorldAxis()
    {
        var layout = ScreenLayout.Create(1280, 720, ScreenInsets.None);
        var camera = new CameraTransform(12);

        var screen = camera.WorldToScreen(layout, 0, 0, 2, 1);

        Assert.Equal(760, screen.X, 3);
        Assert.Equal(300, screen.Y, 3);
    }

    [Fact]
    public void CameraApproachesZoomTargetWithoutJumping()
    {
        var camera = new CameraTransform(12);

        camera.ApproachVisibleWorldHeight(30, 0.1, 0.7);

        Assert.InRange(camera.VisibleWorldHeight, 12.01, 29.99);
        for (var index = 0; index < 70; index++)
            camera.ApproachVisibleWorldHeight(30, 0.1, 0.7);
        Assert.Equal(30, camera.VisibleWorldHeight, 3);
    }
}
