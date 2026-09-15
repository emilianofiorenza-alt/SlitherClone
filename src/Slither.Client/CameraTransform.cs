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

    public double VisibleWorldHeight { get; private set; }

    public void ApproachVisibleWorldHeight(double targetHeight, double elapsedSeconds, double transitionSeconds = 0.7)
    {
        if (!double.IsFinite(targetHeight) || targetHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(targetHeight));
        if (!double.IsFinite(elapsedSeconds) || elapsedSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        if (!double.IsFinite(transitionSeconds) || transitionSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(transitionSeconds));

        // Four time constants put the camera within roughly 2% of the target
        // at the end of the requested transition.
        var amount = 1.0 - Math.Exp((-4.0 * elapsedSeconds) / transitionSeconds);
        VisibleWorldHeight += (targetHeight - VisibleWorldHeight) * amount;
    }

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
