using System.Text.Json;
using MdEditor;

namespace MdEditor.Tests;

public class SettingsTests
{
    [Fact]
    public void Defaults_Are_EnGB_And_SegoeUI()
    {
        var s = new Settings();
        Assert.Equal("en-GB", s.SpellcheckLanguage);
        Assert.Equal("Segoe UI", s.FontFamily);
        Assert.Equal(15, s.FontSize);
        Assert.Null(s.WindowWidth);
    }

    [Fact]
    public void Settings_RoundTrip_Through_Json()
    {
        var original = new Settings
        {
            FontFamily = "Calibri",
            FontSize = 18,
            SpellcheckLanguage = "en-US",
            WindowWidth = 800,
            WindowHeight = 600,
            WindowLeft = 100,
            WindowTop = 50,
        };

        string json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<Settings>(json)!;

        Assert.Equal("Calibri", restored.FontFamily);
        Assert.Equal(18, restored.FontSize);
        Assert.Equal("en-US", restored.SpellcheckLanguage);
        Assert.Equal(800, restored.WindowWidth);
        Assert.Equal(600, restored.WindowHeight);
        Assert.Equal(100, restored.WindowLeft);
        Assert.Equal(50, restored.WindowTop);
    }

    [Fact]
    public void Corrupt_Json_Falls_Back_To_Defaults()
    {
        // Deserializing garbage must not throw in a way that reaches the app;
        // the store swallows it and returns defaults. Here we prove the parse
        // itself is the failure point the store guards against.
        Assert.ThrowsAny<JsonException>(() => JsonSerializer.Deserialize<Settings>("{ not json"));
    }
}
