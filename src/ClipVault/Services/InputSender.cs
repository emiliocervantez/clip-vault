using System.Runtime.InteropServices;
using ClipVault.Native;
using static ClipVault.Native.NativeMethods;

namespace ClipVault.Services;

internal static class InputSender
{
    /// <summary>Sends Ctrl+V to the foreground window, first releasing any modifier the user still holds.</summary>
    public static void SendCtrlV()
    {
        var inputs = new List<INPUT>();
        foreach (var vk in new[] { VK_SHIFT, VK_MENU, VK_LWIN, VK_RWIN })
        {
            if ((GetAsyncKeyState(vk) & 0x8000) != 0) inputs.Add(Key(vk, up: true));
        }
        inputs.Add(Key(VK_CONTROL, up: false));
        inputs.Add(Key(VK_V, up: false));
        inputs.Add(Key(VK_V, up: true));
        inputs.Add(Key(VK_CONTROL, up: true));
        SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<INPUT>());
    }

    private static INPUT Key(int vk, bool up) => new()
    {
        type = INPUT_KEYBOARD,
        u = new INPUTUNION
        {
            ki = new KEYBDINPUT
            {
                wVk = (ushort)vk,
                wScan = (ushort)MapVirtualKey((uint)vk, 0),
                dwFlags = up ? KEYEVENTF_KEYUP : 0,
            },
        },
    };
}
