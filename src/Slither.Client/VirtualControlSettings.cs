namespace Slither.Client;

public sealed record VirtualControlSettings
{
    public static VirtualControlSettings Default { get; } = new();

    public float JoystickBaseRadiusRatio { get; init; } = 0.10f;

    public float JoystickKnobRadiusRatio { get; init; } = 0.42f;

    public float JoystickDeadZoneRatio { get; init; } = 0.15f;

    public float JoystickMarginRatio { get; init; } = 0.05f;

    public float JoystickActivationRadiusMultiplier { get; init; } = 1.55f;

    public float JoystickInactiveAlpha { get; init; } = 0.30f;

    public float JoystickActiveAlpha { get; init; } = 0.50f;

    public float BoostRadiusRatio { get; init; } = 0.08f;

    public float BoostMarginRatio { get; init; } = 0.06f;

    public float BoostTouchRadiusMultiplier { get; init; } = 1.30f;

    public float BoostInactiveAlpha { get; init; } = 0.35f;

    public float BoostPressedAlpha { get; init; } = 0.70f;
}
