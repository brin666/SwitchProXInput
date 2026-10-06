using SwitchProXInput.Core;

var checks = new (string Name, Action Run)[]
{
    ("face buttons follow Xbox positions", FaceButtonsFollowPositions),
    ("shoulders, menu buttons, and stick clicks map", OtherButtonsMap),
    ("ZL/ZR trigger values stay in the analog range", TriggersMap),
    ("radial deadzone centers small stick drift", DeadzoneCentersDrift),
    ("radial deadzone rescales larger stick movement", DeadzoneRescales),
    ("D-pad supports diagonals", DpadSupportsDiagonals),
    ("axis and trigger normalization handles endpoints", NormalizationHandlesEndpoints)
};

var failures = 0;
foreach (var check in checks)
{
    try
    {
        check.Run();
        Console.WriteLine($"PASS {check.Name}");
    }
    catch (Exception exception)
    {
        failures++;
        Console.WriteLine($"FAIL {check.Name}: {exception.Message}");
    }
}

Console.WriteLine($"{checks.Length - failures}/{checks.Length} checks passed.");
return failures == 0 ? 0 : 1;

static void FaceButtonsFollowPositions()
{
    var mapper = new SwitchProMapper(0f);
    var state = mapper.Map(new SwitchInputState(0, 0, 0, 0, 0, 0,
        SwitchButtons.South | SwitchButtons.East | SwitchButtons.West | SwitchButtons.North));
    Equal(XboxButtons.A | XboxButtons.B | XboxButtons.X | XboxButtons.Y, state.Buttons);
}

static void OtherButtonsMap()
{
    var mapper = new SwitchProMapper(0f);
    var state = mapper.Map(new SwitchInputState(0, 0, 0, 0, 0, 0,
        SwitchButtons.LeftShoulder | SwitchButtons.RightShoulder | SwitchButtons.Back |
        SwitchButtons.Start | SwitchButtons.LeftStick | SwitchButtons.RightStick));
    Equal(XboxButtons.LeftBumper | XboxButtons.RightBumper | XboxButtons.View | XboxButtons.Menu |
          XboxButtons.LeftStick | XboxButtons.RightStick, state.Buttons);
}

static void TriggersMap()
{
    var mapper = new SwitchProMapper(0f);
    var state = mapper.Map(new SwitchInputState(0, 0, 0, 0, 0.35f, 1.2f, SwitchButtons.None));
    Near(0.35f, state.LeftTrigger);
    Near(1f, state.RightTrigger);
}

static void DeadzoneCentersDrift()
{
    var (x, y) = SwitchProMapper.ApplyRadialDeadzone(0.05f, 0.05f, 0.12f);
    Near(0f, x);
    Near(0f, y);
}

static void DeadzoneRescales()
{
    var (x, y) = SwitchProMapper.ApplyRadialDeadzone(0.56f, 0f, 0.12f);
    Near(0.5f, x);
    Near(0f, y);

    var (diagonalX, diagonalY) = SwitchProMapper.ApplyRadialDeadzone(1f, 1f, 0.12f);
    Near(0.7071067f, diagonalX);
    Near(0.7071067f, diagonalY);
}

static void DpadSupportsDiagonals()
{
    var mapper = new SwitchProMapper(0f);
    var northEast = mapper.Map(new SwitchInputState(0, 0, 0, 0, 0, 0,
        SwitchButtons.DpadUp | SwitchButtons.DpadRight));
    Equal(DpadDirection.NorthEast, northEast.Dpad);

    var neutral = mapper.Map(new SwitchInputState(0, 0, 0, 0, 0, 0, SwitchButtons.None));
    Equal(DpadDirection.Neutral, neutral.Dpad);
}

static void NormalizationHandlesEndpoints()
{
    Near(-1f, SwitchProMapper.NormalizeSignedAxis(short.MinValue));
    Near(1f, SwitchProMapper.NormalizeSignedAxis(short.MaxValue));
    Near(0f, SwitchProMapper.NormalizeSignedAxis(0));
    Near(0f, SwitchProMapper.NormalizeTriggerAxis(short.MinValue));
    Near(1f, SwitchProMapper.NormalizeTriggerAxis(short.MaxValue));
}

static void Near(float expected, float actual)
{
    if (MathF.Abs(expected - actual) > 0.0001f)
    {
        throw new InvalidOperationException($"Expected {expected}, got {actual}.");
    }
}

static void Equal<T>(T expected, T actual) where T : notnull
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Expected {expected}, got {actual}.");
    }
}
