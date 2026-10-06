using HIDMaestro;
using SwitchProXInput.Core;

namespace SwitchProXInput.App.Services;

public sealed class HidMaestroOutput : IDisposable
{
    private HMContext? _context;
    private HMController? _controller;

    public bool IsActive => _controller is not null;

    public async Task StartAsync(Func<bool> confirmFirstInstall, CancellationToken cancellationToken = default)
    {
        if (IsActive)
        {
            return;
        }

        var context = await Task.Run(() =>
        {
            var created = new HMContext();
            created.LoadDefaultProfiles();
            return created;
        }, cancellationToken);

        try
        {
            if (!context.IsDriverInstalled)
            {
                if (!confirmFirstInstall())
                {
                    throw new OperationCanceledException("用户取消了首次虚拟手柄组件安装。");
                }

                await Task.Run(context.InstallDriver, cancellationToken);
            }

            var profile = context.GetProfile("xbox-360-wired")
                ?? throw new InvalidOperationException("HIDMaestro 未提供 xbox-360-wired 配置。");

            var controller = await Task.Run(
                () => context.CreateController(profile, $"switchpro-xinput-{Guid.NewGuid():N}"),
                cancellationToken);

            _context = context;
            _controller = controller;
            Submit(default);
        }
        catch
        {
            context.Dispose();
            throw;
        }
    }

    public void Submit(in XboxGamepadState state)
    {
        if (_controller is null)
        {
            return;
        }

        var axes = new Dictionary<HMAxis, float>
        {
            [HMAxis.X] = CenteredAxis(state.LeftX),
            [HMAxis.Y] = CenteredAxis(state.LeftY),
            [HMAxis.Rx] = CenteredAxis(state.RightX),
            [HMAxis.Ry] = CenteredAxis(state.RightY),
            [HMAxis.Vx] = SwitchProMapper.NormalizeTrigger(state.LeftTrigger),
            [HMAxis.Vy] = SwitchProMapper.NormalizeTrigger(state.RightTrigger)
        };

        var hidState = new HMGamepadState
        {
            Axes = axes,
            Buttons = MapButtons(state.Buttons),
            Hat = MapHat(state.Dpad)
        };

        _controller.SubmitState(in hidState);
    }

    private static float CenteredAxis(float value) =>
        (Math.Clamp(float.IsFinite(value) ? value : 0f, -1f, 1f) + 1f) / 2f;

    private static HMButton MapButtons(XboxButtons source)
    {
        var result = HMButton.None;
        Add(ref result, source, XboxButtons.A, HMButton.A);
        Add(ref result, source, XboxButtons.B, HMButton.B);
        Add(ref result, source, XboxButtons.X, HMButton.X);
        Add(ref result, source, XboxButtons.Y, HMButton.Y);
        Add(ref result, source, XboxButtons.LeftBumper, HMButton.LeftBumper);
        Add(ref result, source, XboxButtons.RightBumper, HMButton.RightBumper);
        Add(ref result, source, XboxButtons.View, HMButton.Back);
        Add(ref result, source, XboxButtons.Menu, HMButton.Start);
        Add(ref result, source, XboxButtons.LeftStick, HMButton.LeftStick);
        Add(ref result, source, XboxButtons.RightStick, HMButton.RightStick);
        Add(ref result, source, XboxButtons.Guide, HMButton.Guide);
        return result;
    }

    private static void Add(ref HMButton destination, XboxButtons source, XboxButtons sourceButton, HMButton targetButton)
    {
        if ((source & sourceButton) != 0)
        {
            destination |= targetButton;
        }
    }

    private static HMHat MapHat(DpadDirection direction) => direction switch
    {
        DpadDirection.North => HMHat.North,
        DpadDirection.NorthEast => HMHat.NorthEast,
        DpadDirection.East => HMHat.East,
        DpadDirection.SouthEast => HMHat.SouthEast,
        DpadDirection.South => HMHat.South,
        DpadDirection.SouthWest => HMHat.SouthWest,
        DpadDirection.West => HMHat.West,
        DpadDirection.NorthWest => HMHat.NorthWest,
        _ => HMHat.None
    };

    public void Stop()
    {
        _controller = null;
        var context = _context;
        _context = null;
        context?.Dispose();
    }

    public void Dispose() => Stop();
}
