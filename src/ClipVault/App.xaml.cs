using System.Windows;
using ClipVault.Config;
using ClipVault.Core;
using ClipVault.Popup;
using ClipVault.Services;
using ClipVault.Tray;
using CoreSettings = ClipVault.Core.Settings;

namespace ClipVault;

public partial class App : Application
{
    private const string MutexName = @"Local\ClipVault.SingleInstance";
    private const string ShowSettingsEventName = @"Local\ClipVault.ShowSettings";
    private const int AutoInsertDelayMs = 60;
    private const int TemplateRestoreDelayMs = 300;

    private Mutex? _mutex;
    private MessageWindow? _messages;
    private Vault? _vault;
    private HotkeyManager? _hotkeys;
    private HistoryPopup? _popup;
    private TrayIcon? _tray;
    private SettingsWindow? _settingsWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _mutex = new Mutex(true, MutexName, out var isFirst);
        var showSettings = new EventWaitHandle(false, EventResetMode.AutoReset, ShowSettingsEventName);
        if (!isFirst)
        {
            showSettings.Set();   // tell the running instance to open Settings
            Shutdown();
            return;
        }
        ThreadPool.RegisterWaitForSingleObject(showSettings, (_, _) => Dispatcher.BeginInvoke(ShowSettings), null, -1, false);

        var storage = new VaultStorage(VaultStorage.DefaultRoot());
        if (e.Args.Contains("--trace", StringComparer.OrdinalIgnoreCase))
            Trace.Enable(System.IO.Path.Combine(storage.RootDir, "trace.log"), Dispatcher);

        _vault = new Vault(storage);
        _messages = new MessageWindow();
        ClipboardIO.Owner = _messages.Handle;
        _messages.ClipboardUpdated += _vault.CaptureFromClipboard;
        _hotkeys = new HotkeyManager(_messages);
        _popup = new HistoryPopup(_vault, OnClipChosen, OnTemplateChosen);
        _tray = new TrayIcon(
            showHistory: () => _popup.Toggle(),
            showSettings: ShowSettings,
            clearHistory: ClearHistory,
            exit: Shutdown);

        var failures = RegisterHotkeys(_vault.Settings, _vault.Settings);
        if (failures.Count > 0) _tray.Notify("ClipVault hotkeys", string.Join("\n", failures));
    }

    private void ShowSettings()
    {
        if (_settingsWindow is { IsLoaded: true })
        {
            _settingsWindow.Activate();
            return;
        }
        _settingsWindow = new SettingsWindow(_vault!.Settings, ApplySettings);
        _settingsWindow.Show();
    }

    private void ClearHistory()
    {
        var answer = MessageBox.Show("Delete every stored clip?", "ClipVault", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer == MessageBoxResult.Yes) _vault!.History.Clear();
    }

    /// <summary>Applies new settings. Hotkeys that Windows refuses fall back to their previous values.</summary>
    private IReadOnlyList<string> ApplySettings(CoreSettings next)
    {
        var previous = _vault!.Settings;
        var failures = RegisterHotkeys(next, previous);
        _vault.UpdateSettings(next);
        return failures;
    }

    private List<string> RegisterHotkeys(CoreSettings settings, CoreSettings previous)
    {
        var failures = new List<string>();
        _hotkeys!.UnregisterAll();

        if (!_hotkeys.TryRegister(settings.PopupHotkey, () => _popup!.Toggle()))
        {
            var fallback = previous.PopupHotkey;
            if (!fallback.Equals(settings.PopupHotkey) && _hotkeys.TryRegister(fallback, () => _popup!.Toggle()))
            {
                failures.Add($"History hotkey {settings.PopupHotkey} is in use by another application. Kept {fallback}.");
                settings.PopupHotkey = fallback;
            }
            else
            {
                failures.Add($"History hotkey {settings.PopupHotkey} is in use by another application. Open Settings from the tray icon to choose another.");
            }
        }

        foreach (var template in settings.Templates)
        {
            if (template.Hotkey is not { IsEmpty: false } hotkey) continue;
            if (_hotkeys.TryRegister(hotkey, () => InsertTemplate(template))) continue;

            var old = previous.Templates.FirstOrDefault(t => t.Name == template.Name)?.Hotkey;
            if (old is { IsEmpty: false } && !old.Equals(hotkey) && _hotkeys.TryRegister(old, () => InsertTemplate(template)))
            {
                failures.Add($"Hotkey {hotkey} for template \"{template.Name}\" is in use by another application. Kept {old}.");
                template.Hotkey = old;
            }
            else
            {
                failures.Add($"Hotkey {hotkey} for template \"{template.Name}\" is in use by another application. The template has no hotkey now.");
                template.Hotkey = null;
            }
        }
        return failures;
    }

    private async void OnClipChosen(Clip clip, bool insert)
    {
        _vault!.Choose(clip);
        if (!insert) return;
        await Task.Delay(AutoInsertDelayMs);
        Trace.Log($"insert: sending Ctrl+V to {Trace.Foreground()}");
        InputSender.SendCtrlV();
    }

    private void OnTemplateChosen(Template template) => InsertTemplate(template);

    private async void InsertTemplate(Template template)
    {
        var before = ClipboardIO.Read() ?? ClipboardSnapshot.Empty;
        _vault!.SetOwnText(template.Text);
        await Task.Delay(AutoInsertDelayMs);
        InputSender.SendCtrlV();
        await Task.Delay(TemplateRestoreDelayMs);
        _vault.RestoreOwn(before);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _hotkeys?.Dispose();
        _messages?.Dispose();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
