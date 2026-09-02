namespace Slither.Client;

public sealed class ScreenLayout
{
    private ScreenLayout(
        int viewportWidth,
        int viewportHeight,
        ScreenInsets safeArea,
        VirtualControlSettings settings,
        CircleRegion joystickBase,
        CircleRegion joystickActivation,
        float joystickKnobRadius,
        float joystickDeadZoneRadius,
        CircleRegion boostButton,
        CircleRegion boostTouchArea)
    {
        ViewportWidth = viewportWidth;
        ViewportHeight = viewportHeight;
        SafeArea = safeArea;
        Settings = settings;
        JoystickBase = joystickBase;
        JoystickActivation = joystickActivation;
        JoystickKnobRadius = joystickKnobRadius;
        JoystickDeadZoneRadius = joystickDeadZoneRadius;
        BoostButton = boostButton;
        BoostTouchArea = boostTouchArea;
    }

    public int ViewportWidth { get; }

    public int ViewportHeight { get; }

    public ScreenInsets SafeArea { get; }

    public VirtualControlSettings Settings { get; }

    public CircleRegion JoystickBase { get; }

    public CircleRegion JoystickActivation { get; }

    public float JoystickKnobRadius { get; }

    public float JoystickDeadZoneRadius { get; }

    public CircleRegion BoostButton { get; }

    public CircleRegion BoostTouchArea { get; }

    public ScreenPoint GameplayCenter => new(
        SafeArea.Left + ((ViewportWidth - SafeArea.Left - SafeArea.Right) * 0.5f),
        SafeArea.Top + ((ViewportHeight - SafeArea.Top - SafeArea.Bottom) * 0.5f));

    public static ScreenLayout Create(
        int viewportWidth,
        int viewportHeight,
        ScreenInsets safeArea,
        VirtualControlSettings? settings = null)
    {
        if (viewportWidth <= 0 || viewportHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(viewportWidth), "Viewport dimensions must be positive.");
        }

        settings ??= VirtualControlSettings.Default;
        var usableWidth = Math.Max(1, viewportWidth - safeArea.Left - safeArea.Right);
        var usableHeight = Math.Max(1, viewportHeight - safeArea.Top - safeArea.Bottom);
        var shortSide = Math.Min(usableWidth, usableHeight);

        var joystickRadius = shortSide * settings.JoystickBaseRadiusRatio;
        var joystickMargin = shortSide * settings.JoystickMarginRatio;
        var joystickCenter = new ScreenPoint(
            safeArea.Left + joystickMargin + joystickRadius,
            viewportHeight - safeArea.Bottom - joystickMargin - joystickRadius);

        var boostRadius = shortSide * settings.BoostRadiusRatio;
        var boostMargin = shortSide * settings.BoostMarginRatio;
        var boostCenter = new ScreenPoint(
            viewportWidth - safeArea.Right - boostMargin - boostRadius,
            viewportHeight - safeArea.Bottom - boostMargin - boostRadius);

        return new ScreenLayout(
            viewportWidth,
            viewportHeight,
            safeArea,
            settings,
            new CircleRegion(joystickCenter, joystickRadius),
            new CircleRegion(joystickCenter, joystickRadius * settings.JoystickActivationRadiusMultiplier),
            joystickRadius * settings.JoystickKnobRadiusRatio,
            joystickRadius * settings.JoystickDeadZoneRatio,
            new CircleRegion(boostCenter, boostRadius),
            new CircleRegion(boostCenter, boostRadius * settings.BoostTouchRadiusMultiplier));
    }
}
