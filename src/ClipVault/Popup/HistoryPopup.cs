using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClipVault.Core;
using ClipVault.Native;
using ClipVault.Services;

namespace ClipVault.Popup;

/// <summary>
/// The clipboard-history menu. A 1x1 transparent anchor window is placed at the caret and brought to the
/// foreground so the menu can take keyboard input; on close the previously focused window gets focus back.
/// </summary>
internal sealed class HistoryPopup
{
    private const int ThumbnailHeight = 32;
    private const int TooltipImageMax = 400;
    private const double RowFontSize = 13.5;   // default menu font is 12

    private readonly Vault _vault;
    private readonly Action<Clip> _onClipChosen;
    private readonly Action<Template> _onTemplateChosen;
    private readonly Window _anchor;
    private readonly ContextMenu _menu;
    private readonly Dictionary<Guid, BitmapSource> _thumbnails = new();

    private IntPtr _previousWindow;
    private MenuItem? _cancelItem;
    private Clip? _chosenClip;
    private Template? _chosenTemplate;

    public HistoryPopup(Vault vault, Action<Clip> onClipChosen, Action<Template> onTemplateChosen)
    {
        _vault = vault;
        _onClipChosen = onClipChosen;
        _onTemplateChosen = onTemplateChosen;

        _anchor = new Window
        {
            Width = 1,
            Height = 1,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            ShowInTaskbar = false,
            Topmost = true,
            ShowActivated = true,
            Content = new Grid(),
        };

        _menu = new ContextMenu
        {
            Placement = PlacementMode.Relative,
            StaysOpen = false,
        };
        _menu.Closed += OnMenuClosed;
        _menu.PreviewKeyDown += OnMenuKeyDown;
    }

    public bool IsOpen => _menu.IsOpen;

    public void Toggle()
    {
        if (IsOpen) _menu.IsOpen = false;
        else Open();
    }

    private void Open()
    {
        _previousWindow = WindowFocus.Current();
        _chosenClip = null;
        _chosenTemplate = null;
        Trace.Log($"popup open: previous {Trace.Window(_previousWindow)}");

        var pt = WindowFocus.CaretOrCursor(_previousWindow);
        _anchor.Show();
        var hwnd = new WindowInteropHelper(_anchor).Handle;
        NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, pt.X, pt.Y, 0, 0,
            NativeMethods.SWP_NOSIZE | NativeMethods.SWP_SHOWWINDOW);
        WindowFocus.Bring(hwnd);
        Trace.Log($"popup open: anchor shown at {pt.X},{pt.Y}; foreground now {Trace.Foreground()}");

        Populate();
        _menu.PlacementTarget = (UIElement)_anchor.Content;
        _menu.IsOpen = true;
        FocusInitialItem();
        Trace.Log($"popup open: menu open, foreground {Trace.Foreground()}");
    }

    private void Populate()
    {
        _menu.Items.Clear();
        var clips = _vault.History.Items;
        if (clips.Count == 0)
        {
            _menu.Items.Add(EmptyItem());
        }
        for (var i = 0; i < clips.Count; i++)
        {
            _menu.Items.Add(BuildClipItem(clips[i], i + 1));
        }

        _menu.Items.Add(new Separator());
        _menu.Items.Add(BuildTemplateItem());
        _menu.Items.Add(new Separator());
        _cancelItem = new MenuItem { Header = "Cancel" };
        _cancelItem.Click += (_, _) => _menu.IsOpen = false;
        _menu.Items.Add(_cancelItem);
    }

    private static MenuItem EmptyItem() => new() { Header = "(no clips yet)", IsEnabled = false };

    private string NumberPrefix(int number) => _vault.Settings.ShowNumbers ? $"{number:00}. " : "";

    /// <summary>Rewrites the bold number prefix of an existing row.</summary>
    private void SetNumber(MenuItem item, int number)
    {
        var prefix = NumberPrefix(number);
        if (prefix.Length == 0) return;
        switch (item.Header)
        {
            case TextBlock tb when tb.Inlines.FirstInline is Run run:
                run.Text = prefix;
                break;
            case StackPanel sp when sp.Children[0] is TextBlock tb:
                tb.Text = prefix;
                break;
        }
    }

    private MenuItem BuildClipItem(Clip clip, int number)
    {
        var item = new MenuItem { Tag = clip, FontSize = RowFontSize };
        if (clip.Id == _vault.History.LastChosenId) item.FontWeight = FontWeights.Bold;
        var prefix = NumberPrefix(number);

        if (clip.Kind == ClipKind.Text)
        {
            var header = new TextBlock();
            if (prefix.Length > 0) header.Inlines.Add(new Run(prefix) { FontWeight = FontWeights.Bold });
            header.Inlines.Add(new Run(Preview.Row(clip.Text!)));
            item.Header = header;
            if (_vault.Settings.ShowTextHints && Preview.RowIsTruncated(clip.Text!))
            {
                item.ToolTip = new TextBlock
                {
                    Text = Preview.Tooltip(clip.Text!),
                    MaxWidth = 600,
                    TextWrapping = TextWrapping.Wrap,
                };
            }
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
            item.Header = panel;
            if (_vault.Settings.ShowImageHints)
            {
                item.ToolTip = new Image
                {
                    Source = ImageCodec.Load(_vault.Storage.ImagePath(clip)),
                    MaxWidth = TooltipImageMax,
                    MaxHeight = TooltipImageMax,
                    Stretch = Stretch.Uniform,
                };
            }
        }
        ToolTipService.SetInitialShowDelay(item, 700);
        item.Click += (_, _) => { _chosenClip = clip; _menu.IsOpen = false; };
        return item;
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

    private MenuItem BuildTemplateItem()
    {
        var templates = _vault.Settings.Templates;
        var item = new MenuItem { Header = "Template", IsEnabled = templates.Count > 0 };
        foreach (var t in templates)
        {
            var sub = new MenuItem
            {
                Header = new TextBlock { Text = t.Name },
                InputGestureText = t.Hotkey?.ToString(),
                ToolTip = new TextBlock { Text = Preview.Tooltip(t.Text), MaxWidth = 600, TextWrapping = TextWrapping.Wrap },
            };
            sub.Click += (_, _) => { _chosenTemplate = t; _menu.IsOpen = false; };
            item.Items.Add(sub);
        }
        return item;
    }

    private void FocusInitialItem()
    {
        var target = ClipItems().FirstOrDefault();
        if (target is null) return;
        _menu.Dispatcher.BeginInvoke(() => target.Focus(), System.Windows.Threading.DispatcherPriority.Input);
    }

    private IEnumerable<MenuItem> ClipItems() => _menu.Items.OfType<MenuItem>().Where(m => m.Tag is Clip);

    private void OnMenuKeyDown(object sender, KeyEventArgs e)
    {
        var digit = e.Key switch
        {
            >= Key.D1 and <= Key.D9 => e.Key - Key.D0,
            >= Key.NumPad1 and <= Key.NumPad9 => e.Key - Key.NumPad0,
            _ => 0,
        };
        if (digit > 0 && Keyboard.Modifiers == ModifierKeys.None)
        {
            var item = ClipItems().ElementAtOrDefault(digit - 1);
            if (item is not null)
            {
                _chosenClip = (Clip)item.Tag;
                _menu.IsOpen = false;
            }
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Delete)
        {
            DeleteHighlightedRow();
            e.Handled = true;
        }
    }

    /// <summary>
    /// Removes the highlighted clip without rebuilding the menu: rebuilding would drop the focused row,
    /// the menu would lose keyboard focus, and WPF would close it.
    /// </summary>
    private void DeleteHighlightedRow()
    {
        var items = ClipItems().ToList();
        var index = items.FindIndex(m => m.IsHighlighted || m.IsKeyboardFocusWithin);
        if (index < 0) return;

        var victim = items[index];
        var next = items.ElementAtOrDefault(index + 1) ?? items.ElementAtOrDefault(index - 1);
        (next ?? _cancelItem)?.Focus();   // keep focus inside the menu before the row disappears

        _menu.Items.Remove(victim);
        _vault.History.Remove(((Clip)victim.Tag).Id);

        var remaining = ClipItems().ToList();
        if (remaining.Count == 0)
        {
            _menu.Items.Insert(0, EmptyItem());
            return;
        }
        for (var i = 0; i < remaining.Count; i++) SetNumber(remaining[i], i + 1);
    }

    private void OnMenuClosed(object sender, RoutedEventArgs e)
    {
        Trace.Log($"popup closed: chosen={(_chosenClip is not null ? _chosenClip.Kind.ToString() : _chosenTemplate is not null ? "template" : "nothing")}, foreground {Trace.Foreground()}");
        _anchor.Hide();
        Trace.Log($"popup closed: anchor hidden, foreground {Trace.Foreground()}");
        WindowFocus.Bring(_previousWindow);
        Trace.Log($"popup closed: after restore, foreground {Trace.Foreground()}");
        if (Trace.Enabled)
        {
            var later = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            later.Tick += (_, _) => { later.Stop(); Trace.Log($"popup closed +250ms: foreground {Trace.Foreground()}"); };
            later.Start();
        }

        var clip = _chosenClip;
        var template = _chosenTemplate;
        _chosenClip = null;
        _chosenTemplate = null;
        if (clip is not null) _onClipChosen(clip);
        else if (template is not null) _onTemplateChosen(template);
    }
}
