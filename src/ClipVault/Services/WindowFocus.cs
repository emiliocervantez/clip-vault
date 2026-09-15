using ClipVault.Native;
using static ClipVault.Native.NativeMethods;

namespace ClipVault.Services;

/// <summary>Foreground-window bookkeeping and caret lookup.</summary>
internal static class WindowFocus
{
    public static IntPtr Current() => GetForegroundWindow();

    /// <summary>Makes <paramref name="hwnd"/> the foreground window, working around the foreground lock when needed.</summary>
    public static void Bring(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !IsWindow(hwnd)) return;
        if (GetForegroundWindow() == hwnd) return;

        SetForegroundWindow(hwnd);
        if (GetForegroundWindow() == hwnd) return;

        // Windows only grants foreground rights to the thread that received the last input.
        // A synthetic Alt press plus attaching to the current foreground thread unlocks it.
        keybd_event((byte)VK_MENU, 0, 0, UIntPtr.Zero);
        keybd_event((byte)VK_MENU, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

        var foregroundThread = GetWindowThreadProcessId(GetForegroundWindow(), out _);
        var ourThread = GetCurrentThreadId();
        var attached = foregroundThread != 0 && foregroundThread != ourThread && AttachThreadInput(ourThread, foregroundThread, true);
        SetForegroundWindow(hwnd);
        if (attached) AttachThreadInput(ourThread, foregroundThread, false);
    }

    /// <summary>Screen position (pixels) of the text caret in <paramref name="hwnd"/>, or the mouse cursor when no caret is exposed.</summary>
    public static POINT CaretOrCursor(IntPtr hwnd)
    {
        if (hwnd != IntPtr.Zero)
        {
            var thread = GetWindowThreadProcessId(hwnd, out _);
            var info = new GUITHREADINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<GUITHREADINFO>() };
            if (thread != 0 && GetGUIThreadInfo(thread, ref info) && info.hwndCaret != IntPtr.Zero)
            {
                var pt = new POINT { X = info.rcCaret.Left, Y = info.rcCaret.Bottom };
                if (ClientToScreen(info.hwndCaret, ref pt)) return pt;
            }
        }
        GetCursorPos(out var cursor);
        return cursor;
    }
}
