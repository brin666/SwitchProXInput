namespace SwitchProXInput.Core;

[Flags]
public enum SwitchButtons
{
    None = 0,
    South = 1 << 0,
    East = 1 << 1,
    West = 1 << 2,
    North = 1 << 3,
    Back = 1 << 4,
    Guide = 1 << 5,
    Start = 1 << 6,
    LeftStick = 1 << 7,
    RightStick = 1 << 8,
    LeftShoulder = 1 << 9,
    RightShoulder = 1 << 10,
    DpadUp = 1 << 11,
    DpadDown = 1 << 12,
    DpadLeft = 1 << 13,
    DpadRight = 1 << 14
}

public readonly record struct SwitchInputState(
    float LeftX,
    float LeftY,
    float RightX,
    float RightY,
    float LeftTrigger,
    float RightTrigger,
    SwitchButtons Buttons);

[Flags]
public enum XboxButtons
{
    None = 0,
    A = 1 << 0,
    B = 1 << 1,
    X = 1 << 2,
    Y = 1 << 3,
    LeftBumper = 1 << 4,
    RightBumper = 1 << 5,
    View = 1 << 6,
    Menu = 1 << 7,
    LeftStick = 1 << 8,
    RightStick = 1 << 9,
    Guide = 1 << 10
}

public enum DpadDirection
{
    Neutral,
    North,
    NorthEast,
    East,
    SouthEast,
    South,
    SouthWest,
    West,
    NorthWest
}

public readonly record struct XboxGamepadState(
    float LeftX,
    float LeftY,
    float RightX,
    float RightY,
    float LeftTrigger,
    float RightTrigger,
    XboxButtons Buttons,
    DpadDirection Dpad);
