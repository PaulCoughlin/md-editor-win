using System.IO;
using System.Text.Json;

namespace MdEditor;

/// <summary>
/// Loads and saves <see cref="Settings"/> as JSON at
/// %APPDATA%\MdEditor\settings.json. A missing or corrupt file yields defaults
/// rather than an error — preferences must never block the app from starting.
/// </summary>
public static class SettingsStore
{
    private static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MdEditor");

    private static readonly string FilePath = Path.Combine(Dir, "settings.json");

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                string json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
            }
        }
        catch
        {
            // Corrupt or unreadable file → fall back to defaults.
        }
        return new Settings();
    }

    public static void Save(Settings settings)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, Options));
        }
        catch
        {
            // Saving preferences is best-effort; never surface as a crash.
        }
    }
}
