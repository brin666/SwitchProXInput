using System.Runtime.InteropServices;
using SwitchProXInput.Core;

namespace SwitchProXInput.App.Services;

public readonly record struct SwitchInputPoll(bool Connected, string? DeviceName, SwitchInputState State);

/// <summary>Reads Nintendo Switch Pro Controller gamepad state through SDL3.</summary>
public sealed class SdlSwitchProInput : IDisposable
{
    private const uint SdlInitGamepad = 0x00002000;
    private const ushort NintendoVendorId = 0x057E;
    private const ushort SwitchProProductId = 0x2009;
    private static readonly TimeSpan DeviceRescanInterval = TimeSpan.FromMilliseconds(300);

    private IntPtr _gamepad;
    private string? _deviceName;
    private DateTime _nextRescanUtc;
    private bool _disposed;

    public SdlSwitchProInput()
    {
        if (SdlNative.SDL_InitSubSystem(SdlInitGamepad) == 0)
        {
            throw new InvalidOperationException($"SDL 初始化失败：{GetSdlError()}");
        }
    }

    public SwitchInputPoll Poll(DateTime utcNow)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // SDL event pumping must stay on the UI thread. MainWindow calls this from
        // its DispatcherTimer so enumeration and state reads use the same thread.
        SdlNative.SDL_PumpEvents();
        SdlNative.SDL_UpdateGamepads();

        if (_gamepad != IntPtr.Zero && SdlNative.SDL_GamepadConnected(_gamepad) == 0)
        {
            CloseCurrentGamepad();
        }

        if (_gamepad == IntPtr.Zero && utcNow >= _nextRescanUtc)
        {
            _nextRescanUtc = utcNow + DeviceRescanInterval;
            TryOpenSwitchPro();
        }

        if (_gamepad == IntPtr.Zero)
        {
            return new SwitchInputPoll(false, null, default);
        }

        return new SwitchInputPoll(true, _deviceName, ReadState(_gamepad));
    }

    private static SwitchInputState ReadState(IntPtr gamepad)
    {
        var buttons = SwitchButtons.None;
        AddButton(ref buttons, gamepad, SdlButton.South, SwitchButtons.South);
        AddButton(ref buttons, gamepad, SdlButton.East, SwitchButtons.East);
        AddButton(ref buttons, gamepad, SdlButton.West, SwitchButtons.West);
        AddButton(ref buttons, gamepad, SdlButton.North, SwitchButtons.North);
        AddButton(ref buttons, gamepad, SdlButton.Back, SwitchButtons.Back);
        AddButton(ref buttons, gamepad, SdlButton.Guide, SwitchButtons.Guide);
        AddButton(ref buttons, gamepad, SdlButton.Start, SwitchButtons.Start);
        AddButton(ref buttons, gamepad, SdlButton.LeftStick, SwitchButtons.LeftStick);
        AddButton(ref buttons, gamepad, SdlButton.RightStick, SwitchButtons.RightStick);
        AddButton(ref buttons, gamepad, SdlButton.LeftShoulder, SwitchButtons.LeftShoulder);
        AddButton(ref buttons, gamepad, SdlButton.RightShoulder, SwitchButtons.RightShoulder);
        AddButton(ref buttons, gamepad, SdlButton.DpadUp, SwitchButtons.DpadUp);
        AddButton(ref buttons, gamepad, SdlButton.DpadDown, SwitchButtons.DpadDown);
        AddButton(ref buttons, gamepad, SdlButton.DpadLeft, SwitchButtons.DpadLeft);
        AddButton(ref buttons, gamepad, SdlButton.DpadRight, SwitchButtons.DpadRight);

        return new SwitchInputState(
            SwitchProMapper.NormalizeSignedAxis(SdlNative.SDL_GetGamepadAxis(gamepad, SdlAxis.LeftX)),
            SwitchProMapper.NormalizeSignedAxis(SdlNative.SDL_GetGamepadAxis(gamepad, SdlAxis.LeftY)),
            SwitchProMapper.NormalizeSignedAxis(SdlNative.SDL_GetGamepadAxis(gamepad, SdlAxis.RightX)),
            SwitchProMapper.NormalizeSignedAxis(SdlNative.SDL_GetGamepadAxis(gamepad, SdlAxis.RightY)),
            SwitchProMapper.NormalizeTriggerAxis(SdlNative.SDL_GetGamepadAxis(gamepad, SdlAxis.LeftTrigger)),
            SwitchProMapper.NormalizeTriggerAxis(SdlNative.SDL_GetGamepadAxis(gamepad, SdlAxis.RightTrigger)),
            buttons);
    }

    private static void AddButton(ref SwitchButtons destination, IntPtr gamepad, int sdlButton, SwitchButtons mappedButton)
    {
        if (SdlNative.SDL_GetGamepadButton(gamepad, sdlButton) != 0)
        {
            destination |= mappedButton;
        }
    }

    private void TryOpenSwitchPro()
    {
        var ids = SdlNative.SDL_GetGamepads(out var count);
        if (ids == IntPtr.Zero || count <= 0)
        {
            if (ids != IntPtr.Zero)
            {
                SdlNative.SDL_free(ids);
            }

            return;
        }

        try
        {
            for (var index = 0; index < count; index++)
            {
                var instanceId = Marshal.ReadInt32(ids, index * sizeof(int));
                if (SdlNative.SDL_IsGamepad(instanceId) == 0)
                {
                    continue;
                }

                var vendor = SdlNative.SDL_GetGamepadVendorForID(instanceId);
                var product = SdlNative.SDL_GetGamepadProductForID(instanceId);
                var candidateName = Utf8(SdlNative.SDL_GetGamepadNameForID(instanceId));
                if (!IsSwitchPro(vendor, product, candidateName))
                {
                    continue;
                }

                var opened = SdlNative.SDL_OpenGamepad(instanceId);
                if (opened == IntPtr.Zero)
                {
                    continue;
                }

                _gamepad = opened;
                _deviceName = Utf8(SdlNative.SDL_GetGamepadName(opened));
                if (string.IsNullOrWhiteSpace(_deviceName))
                {
                    _deviceName = candidateName ?? "Nintendo Switch Pro Controller";
                }

                break;
            }
        }
        finally
        {
            SdlNative.SDL_free(ids);
        }
    }

    private static bool IsSwitchPro(ushort vendor, ushort product, string? name)
    {
        if (vendor == NintendoVendorId && product == SwitchProProductId)
        {
            return true;
        }

        return name?.Contains("Switch Pro Controller", StringComparison.OrdinalIgnoreCase) == true
            || string.Equals(name, "Pro Controller", StringComparison.OrdinalIgnoreCase);
    }

    private static string? Utf8(IntPtr text) =>
        text == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(text);

    private static string GetSdlError() => Utf8(SdlNative.SDL_GetError()) ?? "未知错误";

    private void CloseCurrentGamepad()
    {
        if (_gamepad != IntPtr.Zero)
        {
            SdlNative.SDL_CloseGamepad(_gamepad);
            _gamepad = IntPtr.Zero;
        }

        _deviceName = null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        CloseCurrentGamepad();
        SdlNative.SDL_QuitSubSystem(SdlInitGamepad);
        _disposed = true;
    }

    private static class SdlAxis
    {
        public const int LeftX = 0;
        public const int LeftY = 1;
        public const int RightX = 2;
        public const int RightY = 3;
        public const int LeftTrigger = 4;
        public const int RightTrigger = 5;
    }

    private static class SdlButton
    {
        public const int South = 0;
        public const int East = 1;
        public const int West = 2;
        public const int North = 3;
        public const int Back = 4;
        public const int Guide = 5;
        public const int Start = 6;
        public const int LeftStick = 7;
        public const int RightStick = 8;
        public const int LeftShoulder = 9;
        public const int RightShoulder = 10;
        public const int DpadUp = 11;
        public const int DpadDown = 12;
        public const int DpadLeft = 13;
        public const int DpadRight = 14;
    }

    private static class SdlNative
    {
        private const string Library = "SDL3";

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern byte SDL_InitSubSystem(uint flags);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern void SDL_QuitSubSystem(uint flags);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern void SDL_PumpEvents();

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern void SDL_UpdateGamepads();

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern IntPtr SDL_GetGamepads(out int count);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern void SDL_free(IntPtr memory);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern byte SDL_IsGamepad(int instanceId);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern IntPtr SDL_GetGamepadNameForID(int instanceId);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern ushort SDL_GetGamepadVendorForID(int instanceId);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern ushort SDL_GetGamepadProductForID(int instanceId);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern IntPtr SDL_OpenGamepad(int instanceId);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern IntPtr SDL_GetGamepadName(IntPtr gamepad);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern byte SDL_GamepadConnected(IntPtr gamepad);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern short SDL_GetGamepadAxis(IntPtr gamepad, int axis);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern byte SDL_GetGamepadButton(IntPtr gamepad, int button);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern void SDL_CloseGamepad(IntPtr gamepad);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern IntPtr SDL_GetError();
    }
}
