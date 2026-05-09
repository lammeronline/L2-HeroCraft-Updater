using System.IO;
using System.Text.Json;

namespace Launcher;

/// <summary>Local player preferences stored beside Launcher.exe.</summary>
public sealed class LauncherSettings
{
    public string ClientDirectory { get; set; } = string.Empty;

    public string ConfigSource { get; set; } = string.Empty;

    public string DisplayMode { get; set; } = "Windowed";

    public string Resolution { get; set; } = "1920x1080";

    public bool AudioMuteOn { get; set; }

    public bool ShowLog { get; set; } = true;

    public DateTimeOffset SavedAt { get; set; } = DateTimeOffset.Now;

    public static async Task<LauncherSettings> LoadAsync(string path)
    {
        if (!File.Exists(path))
        {
            return new LauncherSettings();
        }

        try
        {
            var json = await File.ReadAllTextAsync(path);
            return JsonSerializer.Deserialize<LauncherSettings>(json) ?? new LauncherSettings();
        }
        catch
        {
            // Corrupt local settings should not prevent the launcher from opening.
            return new LauncherSettings();
        }
    }

    public async Task SaveAsync(string path)
    {
        SavedAt = DateTimeOffset.Now;
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json);
    }
}
