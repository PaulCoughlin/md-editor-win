namespace MdEditor;

/// <summary>
/// User preferences. Editor font family/size are a *display* preference only —
/// they are never written into the .md file (markdown has no font concept).
/// </summary>
public class Settings
{
    public string FontFamily { get; set; } = "Segoe UI";
    public double FontSize { get; set; } = 15;

    /// <summary>"en-GB", "en-US", or "off".</summary>
    public string SpellcheckLanguage { get; set; } = "en-GB";

    // Null means "no saved bounds" → use the window's default size / centre.
    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
}
