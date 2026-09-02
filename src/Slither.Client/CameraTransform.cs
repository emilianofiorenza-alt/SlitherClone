namespace Slither.Client;

public sealed class CameraTransform
{
    public const double DefaultVisibleWorldHeight = 12.0;

    public CameraTransform(double visibleWorldHeight = DefaultVisibleWorldHeight)
    {
        if (!double.IsFinite(visibleWorldHeight) || visibleWorldHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(visibleWorldHeight));
        }

        VisibleWorldHeight = visibleWorldHeight;
    }

    public double VisibleWorldHeight { get; }

    public ScreenPoint WorldToScreen(
        ScreenLayout layout,
        double cameraX,
        double cameraY,
        double worldX,
        double worldY)
    {
        var usableHeight = layout.ViewportHeight - layout.SafeArea.Top - layout.SafeArea.Bottom;
        var pixelsPerWorldUnit = usableHeight / VisibleWorldHeight;
        return new ScreenPoint(
            (float)(layout.GameplayCenter.X + ((worldX - cameraX) * pixelsPerWorldUnit)),
            (float)(layout.GameplayCenter.Y - ((worldY - cameraY) * pixelsPerWorldUnit)));
    }
}
