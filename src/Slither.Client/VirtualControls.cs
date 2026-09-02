namespace Slither.Client;

public sealed class VirtualControls
{
    private int? _joystickPointerId;
    private int? _boostPointerId;

    public int? JoystickPointerId => _joystickPointerId;

    public int? BoostPointerId => _boostPointerId;

    public ScreenPoint KnobPosition { get; private set; }

    public double DirectionX { get; private set; }

    public double DirectionY { get; private set; }

    public bool HasDirection { get; private set; }

    public bool Boost { get; private set; }

    public bool JoystickActive => _joystickPointerId.HasValue;

    public void Update(ScreenLayout layout, IReadOnlyList<PointerSample> pointers)
    {
        var joystickPointer = FindPointer(pointers, _joystickPointerId);
        if (_joystickPointerId.HasValue && joystickPointer is null)
        {
            _joystickPointerId = null;
        }

        var boostPointer = FindPointer(pointers, _boostPointerId);
        if (_boostPointerId.HasValue && boostPointer is null)
        {
            _boostPointerId = null;
        }

        foreach (var pointer in pointers)
        {
            if (pointer.Id == _joystickPointerId || pointer.Id == _boostPointerId)
            {
                continue;
            }

            if (!_joystickPointerId.HasValue && layout.JoystickActivation.Contains(pointer.Position))
            {
                _joystickPointerId = pointer.Id;
                joystickPointer = pointer;
                continue;
            }

            if (!_boostPointerId.HasValue && layout.BoostTouchArea.Contains(pointer.Position))
            {
                _boostPointerId = pointer.Id;
                boostPointer = pointer;
            }
        }

        joystickPointer = FindPointer(pointers, _joystickPointerId);
        boostPointer = FindPointer(pointers, _boostPointerId);
        UpdateJoystick(layout, joystickPointer);
        Boost = boostPointer.HasValue;
    }

    private void UpdateJoystick(ScreenLayout layout, PointerSample? pointer)
    {
        var center = layout.JoystickBase.Center;
        if (pointer is null)
        {
            KnobPosition = center;
            DirectionX = 0;
            DirectionY = 0;
            HasDirection = false;
            return;
        }

        var delta = pointer.Value.Position - center;
        var distance = MathF.Sqrt((delta.X * delta.X) + (delta.Y * delta.Y));
        var clampedDistance = Math.Min(distance, layout.JoystickBase.Radius);
        var unitX = distance > float.Epsilon ? delta.X / distance : 0;
        var unitScreenY = distance > float.Epsilon ? delta.Y / distance : 0;
        KnobPosition = new ScreenPoint(
            center.X + (unitX * clampedDistance),
            center.Y + (unitScreenY * clampedDistance));

        if (distance <= layout.JoystickDeadZoneRadius)
        {
            DirectionX = 0;
            DirectionY = 0;
            HasDirection = false;
            return;
        }

        var responseRange = layout.JoystickBase.Radius - layout.JoystickDeadZoneRadius;
        var response = Math.Clamp(
            (clampedDistance - layout.JoystickDeadZoneRadius) / responseRange,
            0,
            1);
        DirectionX = unitX * response;
        DirectionY = -unitScreenY * response;
        HasDirection = true;
    }

    private static PointerSample? FindPointer(IReadOnlyList<PointerSample> pointers, int? pointerId)
    {
        if (!pointerId.HasValue)
        {
            return null;
        }

        for (var index = 0; index < pointers.Count; index++)
        {
            if (pointers[index].Id == pointerId.Value)
            {
                return pointers[index];
            }
        }

        return null;
    }
}
