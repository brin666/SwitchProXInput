param([switch]$Once)

$ErrorActionPreference = 'Stop'

if (-not ('SwitchProXInput.XInputProbe' -as [type])) {
    Add-Type -TypeDefinition @'
using System.Runtime.InteropServices;

namespace SwitchProXInput {
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct XInputGamepad {
        public ushort Buttons;
        public byte LeftTrigger;
        public byte RightTrigger;
        public short LeftX;
        public short LeftY;
        public short RightX;
        public short RightY;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct XInputState {
        public uint PacketNumber;
        public XInputGamepad Gamepad;
    }

    public static class XInputProbe {
        [DllImport("xinput1_4.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern uint XInputGetState(uint userIndex, out XInputState state);
    }
}
'@
}

Write-Host 'Polling XInput slots 0-3. Press Ctrl+C to stop.'
while ($true) {
    $found = $false
    for ($slot = 0; $slot -lt 4; $slot++) {
        $state = [SwitchProXInput.XInputState]::new()
        $result = [SwitchProXInput.XInputProbe]::XInputGetState([uint32]$slot, [ref]$state)
        if ($result -eq 0) {
            $found = $true
            $pad = $state.Gamepad
            $buttons = '0x{0:X4}' -f $pad.Buttons
            Write-Host ("Slot {0}: buttons={1} LT={2,3} RT={3,3} LX={4,6} LY={5,6} RX={6,6} RY={7,6}      `r" -f `
                $slot, $buttons, $pad.LeftTrigger, $pad.RightTrigger, $pad.LeftX, $pad.LeftY, $pad.RightX, $pad.RightY) -NoNewline
        }
    }
    if (-not $found) {
        Write-Host 'No connected XInput controller in slots 0-3.                              ' -NoNewline
    }
    if ($Once) {
        Write-Host ''
        break
    }
    Start-Sleep -Milliseconds 100
}
