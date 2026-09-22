using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClipVault.Core;
using ClipVault.Native;
using ClipVault.Services;
using static ClipVault.Native.NativeMethods;

namespace ClipVault.Popup;

/// <summary>
/// The clipboard-history menu. The window never takes focus; while it is open a low-level keyboard hook
/// feeds it navigation keys and swallows them, so the previously focused application keeps focus and
/// keeps any transient popup it has open.
/// </summary>
internal sealed class HistoryPopup
{
    private const int ThumbnailHeight = 32;
    private const int TooltipImageMax = 400;
    private const int PageStep = 8;

    private readonly Vault _vault;
    /// <summary>Clip plus whether to paste it (false when Shift was held while choosing).</summary>
    private readonly System.Action<Clip, bool> _onClipChosen;
    private readonly PopupWindow _window;
    private readonly PopupInputHooks _hooks;
    private readonly Dictionary<Guid, BitmapSource> _thumbnails = new();

    private IntPtr _previousWindow;

    public HistoryPopup(Vault vault, System.Action<Clip, bool> onClipChosen)
    {
        _vault = vault;
        _onClipChosen = onClipChosen;

        _window = new PopupWindow();
        _window.RowClicked += Activate;

        _hooks = new PopupInputHooks(_window.Dispatcher);
        _hooks.KeyDown += OnKey;
        _hooks.SwitchChord += Close;      // Alt+Tab and friends: close immediately, Windows handles the chord
        _hooks.ClickedOutside += Close;
        _hooks.ForegroundChanged += Close;
    }

    public bool IsOpen => _hooks.Installed;

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    private void Open()
    {
        InputSender.MaskHeldModifiers();   // first thing: the hotkey's Alt/Win must not read as a lone tap to the app underneath
        _previousWindow = WindowFocus.Current();
        Trace.Log($"popup open: previous {Trace.Window(_previousWindow)}");

        var pt = WindowFocus.CaretOrCursor(_previousWindow);
        ShowMain(0);
        _window.ShowAt(pt);
        _hooks.Install(_window.ScreenRect);
        Trace.Log($"popup open: shown at {pt.X},{pt.Y}; foreground still {Trace.Foreground()}");
    }

    private void Close()
    {
        if (!IsOpen) return;
        _hooks.Uninstall();
        _window.Hide();
        Trace.Log($"popup closed: foreground {Trace.Foreground()}");
    }

    // ---- rows ----

    private void ShowMain(int selectedIndex)
    {
        var rows = new List<Row>();
        var clips = _vault.History.Items;
        if (clips.Count == 0) rows.Add(Info("(no clips yet)"));
        for (var i = 0; i < clips.Count; i++) rows.Add(ClipRow(clips[i], i + 1));
        _window.SetRows(rows, selectedIndex);
        if (_window.SelectedRow is null) _window.SelectFirst();
    }

    private static Row Info(string text) => new() { Content = new TextBlock { Text = text }, Selectable = false };

    private static TextBlock TextTooltip(string text) => new()
    {
        Text = Preview.Tooltip(text),
        MaxWidth = 600,
        TextWrapping = TextWrapping.Wrap,
    };

    private string NumberPrefix(int number) => _vault.Settings.ShowNumbers ? $"{number:00}. " : "";

    private Row ClipRow(Clip clip, int number)
    {
        var prefix = NumberPrefix(number);
        object content;
        object? tooltip = null;

        if (clip.Kind == ClipKind.Text)
        {
            var tb = new TextBlock();
            if (prefix.Length > 0) tb.Inlines.Add(new Run(prefix) { FontWeight = FontWeights.Bold });
            tb.Inlines.Add(new Run(Preview.Row(clip.Text!)));
            content = tb;
            if (_vault.Settings.ShowTextHints && Preview.RowIsTruncated(clip.Text!)) tooltip = TextTooltip(clip.Text!);
        }
        else
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            if (prefix.Length > 0)
                panel.Children.Add(new TextBlock { Text = prefix, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center });
            panel.Children.Add(new Image
            {
                Source = Thumbnail(clip),
                Height = ThumbnailHeight,
                Margin = new Thickness(0, 1, 6, 1),
                Stretch = Stretch.Uniform,
            });
            panel.Children.Add(new TextBlock { Text = "(BITMAP)", VerticalAlignment = VerticalAlignment.Center });
            content = panel;
            if (_vault.Settings.ShowImageHints)
            {
                tooltip = new Image
                {
                    Source = ImageCodec.Load(_vault.Storage.ImagePath(clip)),
                    MaxWidth = TooltipImageMax,
                    MaxHeight = TooltipImageMax,
                    Stretch = Stretch.Uniform,
                };
            }
        }

        return new Row
        {
            Content = content,
            Tag = clip,
            ToolTip = tooltip,
            FontSize = _vault.Settings.FontSize,
            FontWeight = clip.Id == _vault.History.LastChosenId ? FontWeights.Bold : FontWeights.Normal,
        };
    }

    private BitmapSource Thumbnail(Clip clip)
    {
        if (!_thumbnails.TryGetValue(clip.Id, out var thumb))
        {
            if (_thumbnails.Count > Settings.MaxItemsLimit) _thumbnails.Clear();
            thumb = ImageCodec.Load(_vault.Storage.ImagePath(clip), ThumbnailHeight);
            _thumbnails[clip.Id] = thumb;
        }
        return thumb;
    }

    // ---- input ----

    private static bool ShiftHeld() => (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0;

    private bool IsPopupHotkey(int vk)
    {
        var hk = _vault.Settings.PopupHotkey;
        if (vk != hk.VirtualKey) return false;
        static bool Down(int key) => (GetAsyncKeyState(key) & 0x8000) != 0;
        var mods = HotkeyModifiers.None;
        if (Down(VK_CONTROL)) mods |= HotkeyModifiers.Control;
        if (Down(VK_MENU)) mods |= HotkeyModifiers.Alt;
        if (Down(VK_SHIFT)) mods |= HotkeyModifiers.Shift;
        if (Down(VK_LWIN) || Down(VK_RWIN)) mods |= HotkeyModifiers.Win;
        return mods == hk.Modifiers;
    }

    private void OnKey(int vk)
    {
        if (!IsOpen) return;
        switch (vk)
        {
            case 0x1B: Close(); return;                                   // Esc
            case 0x26: _window.MoveSelection(-1); return;                 // Up
            case 0x28: _window.MoveSelection(+1); return;                 // Down
            case 0x21: _window.MoveSelection(-PageStep); return;          // PageUp
            case 0x22: _window.MoveSelection(+PageStep); return;          // PageDown
            case 0x24: _window.SelectFirst(); return;                     // Home
            case 0x23: _window.SelectLast(); return;                      // End
            case 0x0D: case 0x20:                                         // Enter, Space
                if (_window.SelectedRow is { } row) Activate(row);
                return;
            case 0x2E:                                                    // Delete
                if (_window.SelectedRow?.Tag is Clip victim) DeleteClip(victim);
                return;
        }

        var digit = vk switch
        {
            >= 0x31 and <= 0x39 => vk - 0x30,
            >= 0x61 and <= 0x69 => vk - 0x60,
            _ => 0,
        };
        if (digit > 0)
        {
            var clips = _vault.History.Items;
            if (digit <= clips.Count) ChooseClip(clips[digit - 1]);
            return;
        }

        if (IsPopupHotkey(vk)) Close();
        // every other key is swallowed by the hook and ignored here, like a menu would
    }

    private void Activate(Row row)
    {
        if (row.Tag is Clip clip) ChooseClip(clip);
    }

    /// <summary>Shift held while choosing means "copy only, do not paste".</summary>
    private void ChooseClip(Clip clip)
    {
        var insert = !ShiftHeld();
        Close();
        _onClipChosen(clip, insert);
    }

    private void DeleteClip(Clip clip)
    {
        var index = _vault.History.Items.ToList().FindIndex(c => c.Id == clip.Id);
        _vault.History.Remove(clip.Id);
        ShowMain(Math.Min(index, Math.Max(0, _vault.History.Items.Count - 1)));
    }
}
