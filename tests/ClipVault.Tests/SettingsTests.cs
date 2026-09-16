using System.Text.Json;
using ClipVault.Core;
using Xunit;

namespace ClipVault.Tests;

public class SettingsTests
{
    [Fact]
    public void Defaults_match_spec()
    {
        var s = new Settings();
        Assert.Equal(50, s.MaxItems);
        Assert.False(s.MoveChosenToTop);
        Assert.Equal("Ctrl+Shift+V", s.PopupHotkey.ToString());
        Assert.True(s.ShowTextHints);
        Assert.True(s.ShowImageHints);
        Assert.True(s.ShowNumbers);
        Assert.Equal(13.5, s.FontSize);
        Assert.Null(s.Validate());
    }

    [Fact]
    public void FontSize_is_validated_cloned_and_serialized()
    {
        Assert.Contains("Font size", new Settings { FontSize = 7 }.Validate());
        Assert.Contains("Font size", new Settings { FontSize = 33 }.Validate());
        var s = new Settings { FontSize = 16 };
        Assert.Null(s.Validate());
        Assert.Equal(16, s.Clone().FontSize);
        Assert.Equal(16, JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(s))!.FontSize);
    }

    [Fact]
    public void Display_settings_survive_clone_and_json()
    {
        var s = new Settings { ShowTextHints = false, ShowImageHints = false, ShowNumbers = false };
        var c = s.Clone();
        Assert.False(c.ShowTextHints);
        Assert.False(c.ShowImageHints);
        Assert.False(c.ShowNumbers);
        var back = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(s))!;
        Assert.False(back.ShowTextHints);
        Assert.False(back.ShowImageHints);
        Assert.False(back.ShowNumbers);
    }

    [Fact]
    public void Hotkey_names()
    {
        Assert.Equal("Ctrl+Alt+F5", new Hotkey(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x74).ToString());
        Assert.Equal("Win+1", new Hotkey(HotkeyModifiers.Win, 0x31).ToString());
        Assert.Equal("Shift+Space", new Hotkey(HotkeyModifiers.Shift, 0x20).ToString());
        Assert.Equal("", new Hotkey().ToString());
    }

    [Fact]
    public void Validate_rejects_duplicate_hotkeys()
    {
        var s = new Settings();
        s.Templates.Add(new Template { Name = "sig", Text = "x", Hotkey = Settings.DefaultPopupHotkey() });
        Assert.Contains("more than once", s.Validate());
    }

    [Fact]
    public void Validate_allows_templates_without_hotkey()
    {
        var s = new Settings();
        s.Templates.Add(new Template { Name = "a", Text = "x" });
        s.Templates.Add(new Template { Name = "b", Text = "y" });
        Assert.Null(s.Validate());
    }

    [Fact]
    public void Validate_rejects_nameless_template_and_bad_count()
    {
        var s = new Settings();
        s.Templates.Add(new Template { Text = "x" });
        Assert.Contains("name", s.Validate());
        s = new Settings { MaxItems = 0 };
        Assert.Contains("between", s.Validate());
    }

    [Fact]
    public void Json_round_trip()
    {
        var s = new Settings { MaxItems = 7, MoveChosenToTop = true };
        s.Templates.Add(new Template { Name = "t", Text = "line1\nline2", Hotkey = new Hotkey(HotkeyModifiers.Win | HotkeyModifiers.Shift, 0x41) });
        var json = JsonSerializer.Serialize(s);
        var back = JsonSerializer.Deserialize<Settings>(json)!;
        Assert.Equal(7, back.MaxItems);
        Assert.True(back.MoveChosenToTop);
        Assert.Equal(s.PopupHotkey, back.PopupHotkey);
        Assert.Equal("Shift+Win+A", back.Templates[0].Hotkey!.ToString());
        Assert.Equal("line1\nline2", back.Templates[0].Text);
    }

    [Fact]
    public void Clone_is_deep()
    {
        var s = new Settings();
        s.Templates.Add(new Template { Name = "t" });
        var c = s.Clone();
        c.Templates[0].Name = "changed";
        c.PopupHotkey.VirtualKey = 1;
        Assert.Equal("t", s.Templates[0].Name);
        Assert.Equal(0x56, s.PopupHotkey.VirtualKey);
    }
}
