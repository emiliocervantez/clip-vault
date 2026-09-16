using ClipVault.Native;
using static ClipVault.Native.NativeMethods;

namespace ClipVault.Services;

/// <summary>Foreground-window bookkeeping and caret lookup. Never changes focus: the popup does not activate.</summary>
internal static class WindowFocus
{
    public static IntPtr Current() => GetForegroundWindow();

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
