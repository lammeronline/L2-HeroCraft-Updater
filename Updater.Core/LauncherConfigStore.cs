using System.Text.Json;

namespace L2ModernUpdater.Core;

/// <summary>Loads and saves launcher config JSON from either local disk or HTTP(S).</summary>
public static class LauncherConfigStore
{
    public static async Task<LauncherConfig> LoadAsync(
        string source,
        HttpClient? httpClient = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            throw new ArgumentException("Config source is required.", nameof(source));
        }

        string json;
        if (Uri.TryCreate(source, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            var client = httpClient ?? new HttpClient();
            json = await client.GetStringAsync(uri, cancellationToken);
        }
        else
        {
            json = await File.ReadAllTextAsync(source, cancellationToken);
        }

        return JsonSerializer.Deserialize<LauncherConfig>(json, ManifestStore.JsonOptions)
            ?? throw new InvalidOperationException("Config is empty or invalid.");
    }

    public static async Task SaveAsync(
        LauncherConfig config,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(outputPath);
        await JsonSerializer.SerializeAsync(stream, config, ManifestStore.JsonOptions, cancellationToken);
        await stream.WriteAsync("\n"u8.ToArray(), cancellationToken);
    }
}
