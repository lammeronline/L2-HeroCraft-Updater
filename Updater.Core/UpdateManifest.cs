using System.Text.Json.Serialization;

namespace L2ModernUpdater.Core;

/// <summary>
/// Patch manifest consumed by the launcher. It describes every managed client file,
/// optional self-update metadata and compatibility fields used by older builds.
/// </summary>
public sealed class UpdateManifest
{
    [JsonPropertyName("version")]
    public string Version { get; init; } = "1.0.0";

    /// <summary>Files that belong to the managed client installation.</summary>
    [JsonPropertyName("files")]
    public List<ManifestFile> Files { get; init; } = [];

    /// <summary>Glob-like patterns ignored when reporting extra client files.</summary>
    [JsonPropertyName("ignore")]
    public List<string> Ignore { get; init; } = [];

    /// <summary>Currently supports "report" or "ignore" for files not present in the manifest.</summary>
    [JsonPropertyName("extraFilePolicy")]
    public string ExtraFilePolicy { get; init; } = "report";

    [JsonPropertyName("launcher")]
    public LauncherUpdateInfo? Launcher { get; init; }
}
