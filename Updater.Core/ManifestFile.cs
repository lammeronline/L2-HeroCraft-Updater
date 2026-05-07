using System.Text.Json.Serialization;

namespace L2ModernUpdater.Core;

public sealed class ManifestFile
{
    [JsonPropertyName("path")]
    public string Path { get; init; } = string.Empty;

    [JsonPropertyName("sha256")]
    public string Sha256 { get; init; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; init; }

    [JsonPropertyName("url")]
    public string Url { get; init; } = string.Empty;

    [JsonPropertyName("compressed")]
    public bool Compressed { get; init; }

    [JsonPropertyName("compressedSize")]
    public long CompressedSize { get; init; }

    [JsonPropertyName("compressedSha256")]
    public string CompressedSha256 { get; init; } = string.Empty;
}
