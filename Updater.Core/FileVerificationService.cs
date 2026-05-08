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
        var results = new List<FileVerificationResult>();
        var total = manifest.Files.Count;

        for (var index = 0; index < total; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var file = manifest.Files[index];
            var localPath = SafePath.CombineUnderRoot(clientDirectory, file.Path);
            progress?.Report(new VerificationProgress
            {
                Completed = index,
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
                continue;
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
                continue;
            }

            if (mode == VerificationMode.Fast)
            {
                continue;
            }

            var sha256 = await FileHasher.ComputeSha256Async(localPath, cancellationToken);
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
        }

        progress?.Report(new VerificationProgress
        {
            Completed = total,
            Total = total,
            CurrentPath = string.Empty
        });

        return results;
    }
}
