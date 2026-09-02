using Slither.Client;

namespace Slither.Tests;

public sealed class VirtualControlsTests
{
    [Fact]
    public void LayoutScalesFromShortSideAndRespectsInsets()
    {
        var layout = ScreenLayout.Create(1200, 800, new ScreenInsets(20, 10, 30, 40));
        var shortSide = 750f;

        Assert.Equal(shortSide * 0.10f, layout.JoystickBase.Radius, 3);
        Assert.Equal(20 + shortSide * 0.15f, layout.JoystickBase.Center.X, 3);
        Assert.Equal(800 - 40 - shortSide * 0.15f, layout.JoystickBase.Center.Y, 3);
        Assert.Equal(1200 - 30 - shortSide * 0.14f, layout.BoostButton.Center.X, 3);
    }

    [Fact]
    public void JoystickInsideDeadZoneHasNoDirection()
    {
        var layout = CreateLayout();
        var controls = new VirtualControls();
        var point = new ScreenPoint(
            layout.JoystickBase.Center.X + layout.JoystickDeadZoneRadius * 0.5f,
            layout.JoystickBase.Center.Y);

        controls.Update(layout, [new PointerSample(10, point)]);

        Assert.True(controls.JoystickActive);
        Assert.False(controls.HasDirection);
        Assert.Equal(0, controls.DirectionX);
        Assert.Equal(0, controls.DirectionY);
    }

    [Fact]
    public void JoystickClampsKnobAndProducesNormalizedDirection()
    {
        var layout = CreateLayout();
        var controls = new VirtualControls();
        var point = new ScreenPoint(
            layout.JoystickBase.Center.X + layout.JoystickBase.Radius * 1.4f,
            layout.JoystickBase.Center.Y);

        controls.Update(layout, [new PointerSample(10, point)]);

        Assert.True(controls.HasDirection);
        Assert.Equal(1, controls.DirectionX, 5);
        Assert.Equal(0, controls.DirectionY, 5);
        Assert.Equal(
            layout.JoystickBase.Center.X + layout.JoystickBase.Radius,
            controls.KnobPosition.X,
            3);
    }

    [Fact]
    public void JoystickAndBoostOwnIndependentPointers()
    {
        var layout = CreateLayout();
        var controls = new VirtualControls();

        controls.Update(layout,
        [
            new PointerSample(4, layout.JoystickBase.Center),
            new PointerSample(7, layout.BoostButton.Center)
        ]);

        Assert.Equal(4, controls.JoystickPointerId);
        Assert.Equal(7, controls.BoostPointerId);
        Assert.True(controls.Boost);

        controls.Update(layout, [new PointerSample(7, layout.BoostButton.Center)]);

        Assert.Null(controls.JoystickPointerId);
        Assert.Equal(7, controls.BoostPointerId);
        Assert.True(controls.Boost);
        Assert.Equal(layout.JoystickBase.Center, controls.KnobPosition);
    }

    [Fact]
    public void AdditionalPointerDoesNotStealJoystickOwnership()
    {
        var layout = CreateLayout();
        var controls = new VirtualControls();
        controls.Update(layout, [new PointerSample(3, layout.JoystickBase.Center)]);

        controls.Update(layout,
        [
            new PointerSample(3, layout.JoystickBase.Center),
            new PointerSample(9, layout.JoystickBase.Center)
        ]);

        Assert.Equal(3, controls.JoystickPointerId);
    }

    private static ScreenLayout CreateLayout() =>
        ScreenLayout.Create(1280, 720, ScreenInsets.None);
}
