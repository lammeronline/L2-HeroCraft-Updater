using System.Text.Json.Serialization;

namespace L2ModernUpdater.Core;

public sealed class LauncherConfig
{
    [JsonPropertyName("manifestUrl")]
    public string ManifestUrl { get; init; } = "https://l2.lammeronline.com/updater/manifest.json";

    [JsonPropertyName("newsUrl")]
    public string NewsUrl { get; init; } = "https://l2.lammeronline.com/updater/news.html";

    [JsonPropertyName("playButtonText")]
    public string PlayButtonText { get; init; } = "PLAY";

    [JsonPropertyName("showClientSettings")]
    public bool ShowClientSettings { get; init; } = true;

    [JsonPropertyName("requireUpdateBeforePlay")]
    public bool RequireUpdateBeforePlay { get; init; }

    [JsonPropertyName("autoLoginEnabled")]
    public bool AutoLoginEnabled { get; init; }

    [JsonPropertyName("gameExecutables")]
    public List<string> GameExecutables { get; init; } = ["system/l2.exe", "l2.exe"];

    [JsonPropertyName("resolutions")]
    public List<string> Resolutions { get; init; } =
    [
        "1024x768",
        "1280x720",
        "1366x768",
        "1600x900",
        "1920x1080",
        "2560x1440"
    ];

    [JsonPropertyName("defaultDisplayMode")]
    public string DefaultDisplayMode { get; init; } = "Windowed";

    [JsonPropertyName("defaultResolution")]
    public string DefaultResolution { get; init; } = "1920x1080";

    [JsonPropertyName("defaultAudioMuteOn")]
    public bool DefaultAudioMuteOn { get; init; }

    public static LauncherConfig CreateDefault()
    {
        return new LauncherConfig();
    }
}
