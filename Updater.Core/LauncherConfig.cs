using System.Text.Json.Serialization;

namespace L2ModernUpdater.Core;

/// <summary>
/// Server-controlled launcher behavior. The launcher loads this file before the manifest,
/// so operators can move manifest/news URLs and disable client-side features without rebuilding.
/// </summary>
public sealed class LauncherConfig
{
    /// <summary>URL or local path to the patch manifest.</summary>
    [JsonPropertyName("manifestUrl")]
    public string ManifestUrl { get; init; } = "https://yoursite.com/updater/manifest.json";

    /// <summary>Optional HTML page shown in the launcher news panel.</summary>
    [JsonPropertyName("newsUrl")]
    public string NewsUrl { get; init; } = "https://yoursite.com/updater/news.html";

    /// <summary>Caption for the main launch button.</summary>
    [JsonPropertyName("playButtonText")]
    public string PlayButtonText { get; init; } = "PLAY";

    /// <summary>Controls whether players can see and edit client video/audio options.</summary>
    [JsonPropertyName("showClientSettings")]
    public bool ShowClientSettings { get; init; } = true;

    /// <summary>When true, Play is blocked until a fast check reports no pending updates.</summary>
    [JsonPropertyName("requireUpdateBeforePlay")]
    public bool RequireUpdateBeforePlay { get; init; }

    /// <summary>Controls visibility of the local AutoLogin account window.</summary>
    [JsonPropertyName("autoLoginEnabled")]
    public bool AutoLoginEnabled { get; init; }

    /// <summary>Relative executable candidates checked in order when starting the game.</summary>
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

    /// <summary>Default values used when no server config can be loaded.</summary>
    public static LauncherConfig CreateDefault()
    {
        return new LauncherConfig();
    }
}
