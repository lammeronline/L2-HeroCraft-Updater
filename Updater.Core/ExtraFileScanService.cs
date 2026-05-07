namespace L2ModernUpdater.Core;

public sealed class ExtraFileScanService
{
    public IReadOnlyList<ExtraFileResult> FindExtraFiles(UpdateManifest manifest, string clientDirectory)
    {
        if (!Directory.Exists(clientDirectory))
        {
            return [];
        }

        var manifestPaths = manifest.Files
            .Select(file => NormalizeManifestPath(file.Path))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var ignoreMatcher = new IgnoreRuleMatcher(manifest.Ignore);

        var extras = new List<ExtraFileResult>();
        foreach (var filePath in Directory.EnumerateFiles(clientDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = NormalizeManifestPath(Path.GetRelativePath(clientDirectory, filePath));
            if (manifestPaths.Contains(relativePath) || ignoreMatcher.IsIgnored(relativePath))
            {
                continue;
            }

            extras.Add(new ExtraFileResult
            {
                RelativePath = relativePath,
                LocalPath = filePath,
                Size = new FileInfo(filePath).Length
            });
        }

        return extras;
    }

    private static string NormalizeManifestPath(string path)
    {
        return path.Replace('\\', '/').TrimStart('/');
    }
}
