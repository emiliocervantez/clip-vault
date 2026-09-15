using System.Windows.Interop;
using ClipVault.Native;

namespace ClipVault.Services;

/// <summary>Hidden top-level window that receives clipboard-update and hotkey messages.</summary>
internal sealed class MessageWindow : IDisposable
{
    private readonly HwndSource _source;

    public MessageWindow()
    {
        var p = new HwndSourceParameters("ClipVaultMessageWindow")
        {
            WindowStyle = 0,   // WS_OVERLAPPED, never shown
            Width = 0,
            Height = 0,
        };
        _source = new HwndSource(p);
        _source.AddHook(Hook);
        NativeMethods.AddClipboardFormatListener(Handle);
    }

    public IntPtr Handle => _source.Handle;

    public event Action? ClipboardUpdated;
    public event Action<int>? HotkeyPressed;

    private IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case NativeMethods.WM_CLIPBOARDUPDATE:
                ClipboardUpdated?.Invoke();
                handled = true;
                break;
            case NativeMethods.WM_HOTKEY:
                HotkeyPressed?.Invoke(wParam.ToInt32());
                handled = true;
                break;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        NativeMethods.RemoveClipboardFormatListener(Handle);
        _source.RemoveHook(Hook);
        _source.Dispose();
    }
}
