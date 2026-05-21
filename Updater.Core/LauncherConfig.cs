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

    /// <summary>Controls visibility of the remote news panel.</summary>
    [JsonPropertyName("showNews")]
    public bool ShowNews { get; init; }

    /// <summary>Controls whether players can see and edit client video/audio options.</summary>
    [JsonPropertyName("showClientSettings")]
    public bool ShowClientSettings { get; init; } = true;

    /// <summary>When true, Play is blocked until a fast check reports no pending updates.</summary>
    [JsonPropertyName("requireUpdateBeforePlay")]
    public bool RequireUpdateBeforePlay { get; init; }

    /// <summary>Controls visibility of the local AutoLogin account window.</summary>
    [JsonPropertyName("autoLoginEnabled")]
    public bool AutoLoginEnabled { get; init; }

    /// <summary>Controls visibility of auth/game server status in the launcher.</summary>
    [JsonPropertyName("showServerStatus")]
    public bool ShowServerStatus { get; init; }

    /// <summary>IP address or host name used for auth/game server status checks.</summary>
    [JsonPropertyName("serverStatusHost")]
    public string ServerStatusHost { get; init; } = "127.0.0.1";

    /// <summary>Lineage II auth server TCP port.</summary>
    [JsonPropertyName("authServerPort")]
    public int AuthServerPort { get; init; } = 2106;

    /// <summary>Lineage II game server TCP port.</summary>
    [JsonPropertyName("gameServerPort")]
    public int GameServerPort { get; init; } = 7777;

    /// <summary>How often the launcher refreshes server status.</summary>
    [JsonPropertyName("serverStatusRefreshSeconds")]
    public int ServerStatusRefreshSeconds { get; init; } = 30;

    /// <summary>TCP connect timeout for one server status check.</summary>
    [JsonPropertyName("serverStatusTimeoutMilliseconds")]
    public int ServerStatusTimeoutMilliseconds { get; init; } = 1500;

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
