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

        var pt = WindowFocus.CaretOrCursor(_previousWindow);
        _anchor.Show();
        var hwnd = new WindowInteropHelper(_anchor).Handle;
        NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, pt.X, pt.Y, 0, 0,
            NativeMethods.SWP_NOSIZE | NativeMethods.SWP_SHOWWINDOW);
        WindowFocus.Bring(hwnd);

        Populate();
        _menu.PlacementTarget = (UIElement)_anchor.Content;
        _menu.IsOpen = true;
        FocusInitialItem();
    }

    private void Populate()
    {
        _menu.Items.Clear();
        var clips = _vault.History.Items;
        if (clips.Count == 0)
        {
            _menu.Items.Add(new MenuItem { Header = "(no clips yet)", IsEnabled = false });
        }
        for (var i = 0; i < clips.Count; i++)
        {
            _menu.Items.Add(BuildClipItem(clips[i], i + 1));
        }

        _menu.Items.Add(new Separator());
        _menu.Items.Add(BuildTemplateItem());
        _menu.Items.Add(new Separator());
        var cancel = new MenuItem { Header = "Cancel" };
        cancel.Click += (_, _) => _menu.IsOpen = false;
        _menu.Items.Add(cancel);
    }

    private MenuItem BuildClipItem(Clip clip, int number)
    {
        var item = new MenuItem { Tag = clip, FontSize = RowFontSize };
        if (clip.Id == _vault.History.LastChosenId) item.FontWeight = FontWeights.Bold;
        var prefix = _vault.Settings.ShowNumbers ? $"{number:00}. " : "";

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
        var target = ClipItems().FirstOrDefault(m => ((Clip)m.Tag).Id == _vault.History.LastChosenId)
                     ?? ClipItems().FirstOrDefault();
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
            var items = ClipItems().ToList();
            var index = items.FindIndex(m => m.IsHighlighted || m.IsKeyboardFocusWithin);
            if (index >= 0)
            {
                _vault.History.Remove(((Clip)items[index].Tag).Id);
                Populate();
                var next = ClipItems().ElementAtOrDefault(Math.Min(index, _vault.History.Items.Count - 1));
                if (next is not null)
                    _menu.Dispatcher.BeginInvoke(() => next.Focus(), System.Windows.Threading.DispatcherPriority.Input);
            }
            e.Handled = true;
        }
    }

    private void OnMenuClosed(object sender, RoutedEventArgs e)
    {
        _anchor.Hide();
        WindowFocus.Bring(_previousWindow);

        var clip = _chosenClip;
        var template = _chosenTemplate;
        _chosenClip = null;
        _chosenTemplate = null;
        if (clip is not null) _onClipChosen(clip);
        else if (template is not null) _onTemplateChosen(template);
    }
}
