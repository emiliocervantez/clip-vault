using System.Drawing;
using System.Windows.Forms;

namespace ClipVault.Tray;

internal sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _icon;

    public TrayIcon(Action showHistory, Action showSettings, Action clearHistory, Action exit)
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Show history", null, (_, _) => showHistory());
        menu.Items.Add("Settings...", null, (_, _) => showSettings());
        menu.Items.Add("Clear history", null, (_, _) => clearHistory());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => exit());

        _icon = new NotifyIcon
        {
            Text = "ClipVault",
            Icon = LoadIcon(),
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.DoubleClick += (_, _) => showSettings();
    }

    public void Notify(string title, string text) => _icon.ShowBalloonTip(5000, title, text, ToolTipIcon.Warning);

    private static Icon LoadIcon()
    {
        try
        {
            if (Environment.ProcessPath is { } exe && Icon.ExtractAssociatedIcon(exe) is { } icon) return icon;
        }
        catch (Exception) { }
        return SystemIcons.Application;
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
