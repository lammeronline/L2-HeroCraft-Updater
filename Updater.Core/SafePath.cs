namespace L2ModernUpdater.Core;

/// <summary>Guards all manifest-driven file access against absolute paths and directory traversal.</summary>
public static class SafePath
{
    public static string CombineUnderRoot(string rootDirectory, string manifestPath)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            throw new ArgumentException("Root directory is required.", nameof(rootDirectory));
        }

        if (string.IsNullOrWhiteSpace(manifestPath))
        {
            throw new ArgumentException("Manifest path is required.", nameof(manifestPath));
        }

        if (Path.IsPathRooted(manifestPath))
        {
            throw new InvalidOperationException($"Manifest path must be relative: {manifestPath}");
        }

        var root = Path.GetFullPath(rootDirectory);
        var combined = Path.GetFullPath(Path.Combine(root, manifestPath.Replace('/', Path.DirectorySeparatorChar)));

        if (!combined.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(combined, root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Manifest path escapes client directory: {manifestPath}");
        }

        return combined;
    }
}
