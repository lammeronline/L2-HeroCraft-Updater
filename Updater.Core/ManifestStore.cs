using System.Text.Json;

namespace L2ModernUpdater.Core;

public static class ManifestStore
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static async Task<UpdateManifest> LoadAsync(
        string source,
        HttpClient? httpClient = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            throw new ArgumentException("Manifest source is required.", nameof(source));
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

        return JsonSerializer.Deserialize<UpdateManifest>(json, JsonOptions)
            ?? throw new InvalidOperationException("Manifest is empty or invalid.");
    }

    public static async Task SaveAsync(
        UpdateManifest manifest,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(outputPath);
        await JsonSerializer.SerializeAsync(stream, manifest, JsonOptions, cancellationToken);
        await stream.WriteAsync("\n"u8.ToArray(), cancellationToken);
    }
}
