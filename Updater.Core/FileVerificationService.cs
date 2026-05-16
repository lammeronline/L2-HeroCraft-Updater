using System.Collections.Concurrent;

namespace L2ModernUpdater.Core;

/// <summary>
/// Compares the local client folder with a manifest. Fast mode trusts size only;
/// full mode also verifies SHA256 for every existing file.
/// </summary>
public sealed class FileVerificationService
{
    public async Task<IReadOnlyList<FileVerificationResult>> VerifyAsync(
        UpdateManifest manifest,
        string clientDirectory,
        VerificationMode mode,
        IProgress<VerificationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var total = manifest.Files.Count;
        var completed = 0;
        var results = new ConcurrentBag<FileVerificationResult>();

        await Parallel.ForEachAsync(
            manifest.Files,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, 8),
                CancellationToken = cancellationToken
            },
            async (file, ct) =>
            {
                var localPath = SafePath.CombineUnderRoot(clientDirectory, file.Path);
                progress?.Report(new VerificationProgress
                {
                    Completed = Interlocked.Increment(ref completed),
                    Total = total,
                    CurrentPath = file.Path
                });

                if (!File.Exists(localPath))
                {
                    results.Add(new FileVerificationResult
                    {
                        File = file,
                        LocalPath = localPath,
                        State = VerificationState.Missing
                    });
                    return;
                }

                var info = new FileInfo(localPath);
                if (info.Length != file.Size)
                {
                    results.Add(new FileVerificationResult
                    {
                        File = file,
                        LocalPath = localPath,
                        State = VerificationState.Outdated
                    });
                    return;
                }

                if (mode == VerificationMode.Fast)
                {
                    return;
                }

                var sha256 = await FileHasher.ComputeSha256Async(localPath, ct);
                if (!string.Equals(sha256, file.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(new FileVerificationResult
                    {
                        File = file,
                        LocalPath = localPath,
                        State = VerificationState.Outdated,
                        ActualSha256 = sha256
                    });
                }
            });

        progress?.Report(new VerificationProgress { Completed = total, Total = total, CurrentPath = string.Empty });
        return results.ToList();
    }
}
