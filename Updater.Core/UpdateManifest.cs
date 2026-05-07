using System.Text.Json.Serialization;

namespace L2ModernUpdater.Core;

public sealed class UpdateManifest
{
    [JsonPropertyName("version")]
    public string Version { get; init; } = "1.0.0";

    [JsonPropertyName("files")]
    public List<ManifestFile> Files { get; init; } = [];

    [JsonPropertyName("ignore")]
    public List<string> Ignore { get; init; } = [];

    [JsonPropertyName("extraFilePolicy")]
    public string ExtraFilePolicy { get; init; } = "report";

    [JsonPropertyName("launcher")]
    public LauncherUpdateInfo? Launcher { get; init; }

    [JsonPropertyName("news")]
    public List<NewsItem> News { get; init; } = [];
}
