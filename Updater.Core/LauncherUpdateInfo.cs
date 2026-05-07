using System.Text.Json.Serialization;

namespace L2ModernUpdater.Core;

public sealed class LauncherUpdateInfo
{
    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;

    [JsonPropertyName("sha256")]
    public string Sha256 { get; init; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; init; }

    [JsonPropertyName("url")]
    public string Url { get; init; } = string.Empty;
}
