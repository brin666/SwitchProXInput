namespace SwitchProXInput.Core;

/// <summary>Maps SDL's position-based gamepad state to an Xbox-style layout.</summary>
public sealed class SwitchProMapper
{
    public SwitchProMapper(float deadzone = 0.12f)
    {
        Deadzone = Math.Clamp(deadzone, 0f, 0.5f);
    }

    public float Deadzone { get; }

    public XboxGamepadState Map(in SwitchInputState input)
    {
        var (leftX, leftY) = ApplyRadialDeadzone(input.LeftX, input.LeftY, Deadzone);
        var (rightX, rightY) = ApplyRadialDeadzone(input.RightX, input.RightY, Deadzone);

        XboxButtons buttons = XboxButtons.None;
        Add(ref buttons, input.Buttons, SwitchButtons.South, XboxButtons.A);
        Add(ref buttons, input.Buttons, SwitchButtons.East, XboxButtons.B);
        Add(ref buttons, input.Buttons, SwitchButtons.West, XboxButtons.X);
        Add(ref buttons, input.Buttons, SwitchButtons.North, XboxButtons.Y);
        Add(ref buttons, input.Buttons, SwitchButtons.LeftShoulder, XboxButtons.LeftBumper);
        Add(ref buttons, input.Buttons, SwitchButtons.RightShoulder, XboxButtons.RightBumper);
        Add(ref buttons, input.Buttons, SwitchButtons.Back, XboxButtons.View);
        Add(ref buttons, input.Buttons, SwitchButtons.Start, XboxButtons.Menu);
        Add(ref buttons, input.Buttons, SwitchButtons.LeftStick, XboxButtons.LeftStick);
        Add(ref buttons, input.Buttons, SwitchButtons.RightStick, XboxButtons.RightStick);
        Add(ref buttons, input.Buttons, SwitchButtons.Guide, XboxButtons.Guide);

        return new XboxGamepadState(
            leftX,
            leftY,
            rightX,
            rightY,
            NormalizeTrigger(input.LeftTrigger),
            NormalizeTrigger(input.RightTrigger),
            buttons,
            MapDpad(input.Buttons));
    }

    /// <summary>Rescales a signed SDL axis into [-1, 1], including its asymmetric endpoints.</summary>
    public static float NormalizeSignedAxis(short value) =>
        value < 0 ? value / 32768f : value / 32767f;

    /// <summary>SDL gamepad trigger axes use 0 for released and 32767 for fully pressed.</summary>
    public static float NormalizeTriggerAxis(short value) =>
        Math.Clamp(value / 32767f, 0f, 1f);

    public static float NormalizeTrigger(float value) =>
        float.IsFinite(value) ? Math.Clamp(value, 0f, 1f) : 0f;

    public static (float X, float Y) ApplyRadialDeadzone(float x, float y, float deadzone)
    {
        x = ClampAxis(x);
        y = ClampAxis(y);
        deadzone = Math.Clamp(deadzone, 0f, 0.5f);

        var magnitude = MathF.Sqrt((x * x) + (y * y));
        if (!float.IsFinite(magnitude) || magnitude <= deadzone || magnitude == 0f)
        {
            return (0f, 0f);
        }

        var clampedMagnitude = MathF.Min(magnitude, 1f);
        var adjustedMagnitude = (clampedMagnitude - deadzone) / (1f - deadzone);
        var scale = adjustedMagnitude / magnitude;
        return (x * scale, y * scale);
    }

    private static float ClampAxis(float value) =>
        float.IsFinite(value) ? Math.Clamp(value, -1f, 1f) : 0f;

    private static void Add(ref XboxButtons target, SwitchButtons source, SwitchButtons sourceButton, XboxButtons targetButton)
    {
        if ((source & sourceButton) != 0)
        {
            target |= targetButton;
        }
    }

    private static DpadDirection MapDpad(SwitchButtons buttons)
    {
        var up = (buttons & SwitchButtons.DpadUp) != 0;
        var down = (buttons & SwitchButtons.DpadDown) != 0;
        var left = (buttons & SwitchButtons.DpadLeft) != 0;
        var right = (buttons & SwitchButtons.DpadRight) != 0;

        if (up && left) return DpadDirection.NorthWest;
        if (up && right) return DpadDirection.NorthEast;
        if (down && left) return DpadDirection.SouthWest;
        if (down && right) return DpadDirection.SouthEast;
        if (up) return DpadDirection.North;
        if (down) return DpadDirection.South;
        if (left) return DpadDirection.West;
        if (right) return DpadDirection.East;
        return DpadDirection.Neutral;
    }
}
