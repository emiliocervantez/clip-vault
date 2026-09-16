using ClipVault.Native;
using static ClipVault.Native.NativeMethods;

namespace ClipVault.Services;

/// <summary>Foreground-window bookkeeping and caret lookup.</summary>
internal static class WindowFocus
{
    public static IntPtr Current() => GetForegroundWindow();

    /// <summary>
    /// Makes <paramref name="hwnd"/> the foreground window. Plain SetForegroundWindow only: on open the
    /// hotkey grants foreground rights, on close this process already is the foreground process.
    /// Cross-process activation is asynchronous, so the result is not verified here.
    /// No AttachThreadInput and no synthetic key press: both stall Electron apps (Slack, VS Code)
    /// for several seconds or put them into menu-bar mode.
    /// </summary>
    public static void Bring(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !IsWindow(hwnd)) return;
        if (GetForegroundWindow() == hwnd) return;
        SetForegroundWindow(hwnd);
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
