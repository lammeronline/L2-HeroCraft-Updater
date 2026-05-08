using System.Text.Json.Serialization;

namespace L2ModernUpdater.Core;

/// <summary>
/// One managed file in the patch manifest. Paths are always relative to the client root
/// and are validated through <see cref="SafePath"/> before disk access.
/// </summary>
public sealed class ManifestFile
{
    /// <summary>Relative client path, using forward slashes in JSON.</summary>
    [JsonPropertyName("path")]
    public string Path { get; init; } = string.Empty;

    /// <summary>SHA256 of the final uncompressed file.</summary>
    [JsonPropertyName("sha256")]
    public string Sha256 { get; init; } = string.Empty;

    /// <summary>Final uncompressed file size in bytes.</summary>
    [JsonPropertyName("size")]
    public long Size { get; init; }

    /// <summary>HTTP(S), file URI or absolute local path used as the download source.</summary>
    [JsonPropertyName("url")]
    public string Url { get; init; } = string.Empty;

    /// <summary>True when <see cref="Url"/> points to a .gz payload.</summary>
    [JsonPropertyName("compressed")]
    public bool Compressed { get; init; }

    /// <summary>Compressed payload size in bytes, when compression is enabled.</summary>
    [JsonPropertyName("compressedSize")]
    public long CompressedSize { get; init; }

    /// <summary>SHA256 of the compressed payload, checked before decompression.</summary>
    [JsonPropertyName("compressedSha256")]
    public string CompressedSha256 { get; init; } = string.Empty;
}
