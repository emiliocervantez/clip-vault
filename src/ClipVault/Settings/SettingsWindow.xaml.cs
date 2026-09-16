using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using ClipVault.Core;
using CoreSettings = ClipVault.Core.Settings;

namespace ClipVault.Config;

public partial class SettingsWindow : Window
{
    private readonly CoreSettings _draft;
    private readonly ObservableCollection<Template> _templates;
    private readonly Func<CoreSettings, IReadOnlyList<string>> _apply;
    private Template? _current;
    private bool _loading;

    /// <param name="apply">Applies the settings and returns hotkey problems, if any.</param>
    internal SettingsWindow(CoreSettings current, Func<CoreSettings, IReadOnlyList<string>> apply)
    {
        InitializeComponent();
        _draft = current.Clone();
        _apply = apply;
        _templates = new ObservableCollection<Template>(_draft.Templates);

        MaxItemsBox.Text = _draft.MaxItems.ToString();
        MaxItemsRange.Text = $"({CoreSettings.MinItems}-{CoreSettings.MaxItemsLimit})";
        FontSizeBox.Text = _draft.FontSize.ToString(CultureInfo.InvariantCulture);
        FontSizeRange.Text = $"({CoreSettings.MinFontSize}-{CoreSettings.MaxFontSize})";
        MoveToTopBox.IsChecked = _draft.MoveChosenToTop;
        StartupBox.IsChecked = _draft.StartWithWindows;
        TextHintsBox.IsChecked = _draft.ShowTextHints;
        ImageHintsBox.IsChecked = _draft.ShowImageHints;
        NumbersBox.IsChecked = _draft.ShowNumbers;
        PopupHotkeyBox.Value = _draft.PopupHotkey;
        TemplateList.ItemsSource = _templates;
        TemplateHotkeyBox.ValueChanged += () => { if (_current is not null) _current.Hotkey = TemplateHotkeyBox.Value; };
    }

    private void TemplateList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _current = TemplateList.SelectedItem as Template;
        _loading = true;
        TemplateEditor.IsEnabled = _current is not null;
        TemplateNameBox.Text = _current?.Name ?? "";
        TemplateTextBox.Text = _current?.Text ?? "";
        TemplateHotkeyBox.Value = _current?.Hotkey;
        _loading = false;
    }

    private void AddTemplate_Click(object sender, RoutedEventArgs e)
    {
        var t = new Template { Name = $"Template {_templates.Count + 1}" };
        _templates.Add(t);
        TemplateList.SelectedItem = t;
        TemplateNameBox.Focus();
        TemplateNameBox.SelectAll();
    }

    private void RemoveTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (_current is not null) _templates.Remove(_current);
    }

    private void TemplateName_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading || _current is null) return;
        _current.Name = TemplateNameBox.Text;
        TemplateList.Items.Refresh();
    }

    private void TemplateText_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading || _current is null) return;
        _current.Text = TemplateTextBox.Text;
    }

    // IsCancel only closes windows opened with ShowDialog; this one is opened with Show.
    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(MaxItemsBox.Text.Trim(), out var maxItems))
        {
            MessageBox.Show(this, "Number of stored items must be a whole number.", "ClipVault", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (!double.TryParse(FontSizeBox.Text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var fontSize))
        {
            MessageBox.Show(this, "Popup font size must be a number.", "ClipVault", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        _draft.MaxItems = maxItems;
        _draft.FontSize = fontSize;
        _draft.MoveChosenToTop = MoveToTopBox.IsChecked == true;
        _draft.StartWithWindows = StartupBox.IsChecked == true;
        _draft.ShowTextHints = TextHintsBox.IsChecked == true;
        _draft.ShowImageHints = ImageHintsBox.IsChecked == true;
        _draft.ShowNumbers = NumbersBox.IsChecked == true;
        _draft.PopupHotkey = PopupHotkeyBox.Value ?? new Hotkey();
        _draft.Templates = _templates.ToList();

        if (_draft.Validate() is { } problem)
        {
            MessageBox.Show(this, problem, "ClipVault", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var failures = _apply(_draft);
        if (failures.Count > 0)
        {
            MessageBox.Show(this, string.Join("\n\n", failures), "ClipVault - hotkeys", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        Close();
    }
}
