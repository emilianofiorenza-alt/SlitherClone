namespace Slither.Client;

public sealed record VirtualControlSettings
{
    public static VirtualControlSettings Default { get; } = new();

    public float JoystickBaseRadiusRatio { get; init; } = 0.075f;

    public float JoystickKnobRadiusRatio { get; init; } = 0.42f;

    public float JoystickDeadZoneRatio { get; init; } = 0.15f;

    public float JoystickHorizontalCenterInsetRatio { get; init; } = 0.255f;

    public float JoystickBottomCenterInsetRatio { get; init; } = 0.25f;

    public float JoystickActivationRadiusMultiplier { get; init; } = 1.55f;

    public float JoystickInactiveAlpha { get; init; } = 0.30f;

    public float JoystickActiveAlpha { get; init; } = 0.50f;

    public float BoostRadiusRatio { get; init; } = 0.065f;

    public float BoostHorizontalCenterInsetRatio { get; init; } = 0.245f;

    public float BoostBottomCenterInsetRatio { get; init; } = 0.25f;

    public float BoostTouchRadiusMultiplier { get; init; } = 1.30f;

    public float BoostInactiveAlpha { get; init; } = 0.35f;

    public float BoostPressedAlpha { get; init; } = 0.70f;
}
