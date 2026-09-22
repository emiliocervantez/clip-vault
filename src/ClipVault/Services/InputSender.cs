using System.Runtime.InteropServices;
using ClipVault.Native;
using static ClipVault.Native.NativeMethods;

namespace ClipVault.Services;

internal static class InputSender
{
    private const uint GUI_INMENUMODE = 0x0004;
    private const int MenuModeExitDelayMs = 30;

    private static bool Down(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

    /// <summary>
    /// Call right after a hotkey fired. A Win32 app that sees Alt (or Win) go down and then up with no other
    /// key in between enters menu-bar keyboard mode (or opens Start), and the next Ctrl+V lands there instead of
    /// in the editor. The app never sees the hotkey's own key, so tap Ctrl while the modifier is still held.
    /// </summary>
    public static void MaskHeldModifiers()
    {
        if (!(Down(VK_MENU) || Down(VK_LWIN) || Down(VK_RWIN))) return;
        Send(Key(VK_CONTROL, up: false), Key(VK_CONTROL, up: true));
        Trace.Log("input: masked held Alt/Win with a Ctrl tap");
    }

    /// <summary>Sends Ctrl+V to the foreground window.</summary>
    public static void SendCtrlV()
    {
        LeaveMenuModeIfNeeded();

        // Ctrl goes down first: releasing a still-held Alt/Win while Ctrl is down is not a "lone" release.
        var inputs = new List<INPUT> { Key(VK_CONTROL, up: false) };
        foreach (var vk in new[] { VK_SHIFT, VK_MENU, VK_LWIN, VK_RWIN })
        {
            if (Down(vk)) inputs.Add(Key(vk, up: true));
        }
        inputs.Add(Key(VK_V, up: false));
        inputs.Add(Key(VK_V, up: true));
        inputs.Add(Key(VK_CONTROL, up: true));
        Send(inputs.ToArray());
    }

    /// <summary>Fallback: if the foreground app is already in menu-bar mode, Esc gets it back to its editor.</summary>
    private static void LeaveMenuModeIfNeeded()
    {
        var thread = GetWindowThreadProcessId(GetForegroundWindow(), out _);
        var info = new GUITHREADINFO { cbSize = Marshal.SizeOf<GUITHREADINFO>() };
        if (thread == 0 || !GetGUIThreadInfo(thread, ref info) || (info.flags & GUI_INMENUMODE) == 0) return;
        Trace.Log("input: foreground is in menu mode, sending Esc first");
        Send(Key(VK_ESCAPE, up: false), Key(VK_ESCAPE, up: true));
        Thread.Sleep(MenuModeExitDelayMs);
    }

    private static void Send(params INPUT[] inputs) => SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());

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
