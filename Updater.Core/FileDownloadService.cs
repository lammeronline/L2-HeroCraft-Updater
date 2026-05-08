using System.Net;
using System.Net.Http.Headers;
using System.IO.Compression;

namespace L2ModernUpdater.Core;

/// <summary>
/// Downloads or copies missing/outdated manifest files, verifies hashes and reports both
/// current-file and total progress. Temporary files are moved into place only after validation.
/// </summary>
public sealed class FileDownloadService
{
    private readonly HttpClient _httpClient;

    public FileDownloadService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task DownloadAsync(
        IReadOnlyList<FileVerificationResult> files,
        string clientDirectory,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var totalFiles = files.Count;
        var totalBytes = files.Sum(file => GetTransferSize(file.File));
        long completedBytes = 0;

        for (var index = 0; index < totalFiles; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var item = files[index];
            var outputPath = SafePath.CombineUnderRoot(clientDirectory, item.File.Path);
            var tempPath = outputPath + ".download";
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

            if (item.File.Compressed)
            {
                await DownloadCompressedAsync(
                    item,
                    outputPath,
                    tempPath,
                    index,
                    totalFiles,
                    completedBytes,
                    totalBytes,
                    progress,
                    cancellationToken);

                File.Move(tempPath, outputPath, true);
                completedBytes += GetTransferSize(item.File);
                progress?.Report(new DownloadProgress
                {
                    CompletedFiles = index + 1,
                    TotalFiles = totalFiles,
                    CompletedBytes = completedBytes,
                    TotalBytes = totalBytes,
                    CurrentFileCompletedBytes = GetTransferSize(item.File),
                    CurrentFileTotalBytes = GetTransferSize(item.File),
                    CurrentPath = item.File.Path
                });
                continue;
            }

            var existingBytes = File.Exists(tempPath) ? new FileInfo(tempPath).Length : 0;
            var downloadedForCurrentFile = existingBytes;
            if (existingBytes > item.File.Size)
            {
                File.Delete(tempPath);
                existingBytes = 0;
                downloadedForCurrentFile = 0;
            }

            if (TryGetLocalSourcePath(item.File.Url, out var localSourcePath))
            {
                await CopyLocalFileAsync(
                    item,
                    localSourcePath,
                    tempPath,
                    index,
                    totalFiles,
                    completedBytes,
                    totalBytes,
                    progress,
                    cancellationToken);

                File.Move(tempPath, outputPath, true);
                completedBytes += item.File.Size;
                progress?.Report(new DownloadProgress
                {
                    CompletedFiles = index + 1,
                    TotalFiles = totalFiles,
                    CompletedBytes = completedBytes,
                    TotalBytes = totalBytes,
                    CurrentFileCompletedBytes = item.File.Size,
                    CurrentFileTotalBytes = item.File.Size,
                    CurrentPath = item.File.Path
                });
                continue;
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, item.File.Url);
            if (existingBytes > 0)
            {
                request.Headers.Range = new RangeHeaderValue(existingBytes, null);
            }

            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (existingBytes > 0 && response.StatusCode != HttpStatusCode.PartialContent)
            {
                File.Delete(tempPath);
                existingBytes = 0;
                downloadedForCurrentFile = 0;
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}: {item.File.Url}");
            }

            await using (var input = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var output = new FileStream(tempPath, FileMode.Append, FileAccess.Write, FileShare.None, 1024 * 128, true))
            {
                var buffer = new byte[1024 * 128];
                int read;
                while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    downloadedForCurrentFile += read;
                    progress?.Report(new DownloadProgress
                    {
                        CompletedFiles = index,
                        TotalFiles = totalFiles,
                        CompletedBytes = completedBytes + downloadedForCurrentFile,
                        TotalBytes = totalBytes,
                        CurrentFileCompletedBytes = downloadedForCurrentFile,
                        CurrentFileTotalBytes = item.File.Size,
                        CurrentPath = item.File.Path
                    });
                }
            }

            var actualSha256 = await FileHasher.ComputeSha256Async(tempPath, cancellationToken);
            if (!string.Equals(actualSha256, item.File.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Downloaded file failed SHA256 verification: {item.File.Path}");
            }

            File.Move(tempPath, outputPath, true);
            completedBytes += GetTransferSize(item.File);
            progress?.Report(new DownloadProgress
            {
                CompletedFiles = index + 1,
                TotalFiles = totalFiles,
                CompletedBytes = completedBytes,
                TotalBytes = totalBytes,
                CurrentFileCompletedBytes = GetTransferSize(item.File),
                CurrentFileTotalBytes = GetTransferSize(item.File),
                CurrentPath = item.File.Path
            });
        }
    }

    private async Task DownloadCompressedAsync(
        FileVerificationResult item,
        string outputPath,
        string tempPath,
        int fileIndex,
        int totalFiles,
        long completedBytesBeforeFile,
        long totalBytes,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        var compressedTempPath = outputPath + ".download.gz";

        await DownloadSourceToFileAsync(
            item.File.Url,
            compressedTempPath,
            item.File.Path,
            fileIndex,
            totalFiles,
            completedBytesBeforeFile,
            totalBytes,
            progress,
            cancellationToken);

        if (item.File.CompressedSize > 0)
        {
            var actualSize = new FileInfo(compressedTempPath).Length;
            if (actualSize != item.File.CompressedSize)
            {
                throw new InvalidOperationException($"Compressed file size mismatch: {item.File.Path}");
            }
        }

        var compressedSha256 = await FileHasher.ComputeSha256Async(compressedTempPath, cancellationToken);
        if (!string.Equals(compressedSha256, item.File.CompressedSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Compressed file failed SHA256 verification: {item.File.Path}");
        }

        if (File.Exists(tempPath))
        {
            File.Delete(tempPath);
        }

        await using (var compressedInput = new FileStream(compressedTempPath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 128, true))
        await using (var gzip = new GZipStream(compressedInput, CompressionMode.Decompress))
        await using (var output = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 128, true))
        {
            await gzip.CopyToAsync(output, cancellationToken);
        }

        var actualSha256 = await FileHasher.ComputeSha256Async(tempPath, cancellationToken);
        if (!string.Equals(actualSha256, item.File.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Decompressed file failed SHA256 verification: {item.File.Path}");
        }

        File.Delete(compressedTempPath);
    }

    private async Task DownloadSourceToFileAsync(
        string sourceUrl,
        string outputPath,
        string displayPath,
        int fileIndex,
        int totalFiles,
        long completedBytesBeforeFile,
        long totalBytes,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }

        if (TryGetLocalSourcePath(sourceUrl, out var localSourcePath))
        {
            if (!File.Exists(localSourcePath))
            {
                throw new FileNotFoundException($"Patch source file not found: {localSourcePath}", localSourcePath);
            }

            await using var input = new FileStream(localSourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 128, true);
            await using var output = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 128, true);
            await CopyWithProgressAsync(input, output, displayPath, fileIndex, totalFiles, completedBytesBeforeFile, totalBytes, progress, cancellationToken);
            return;
        }

        using var response = await _httpClient.GetAsync(sourceUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}: {sourceUrl}");
        }

        await using var httpInput = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var httpOutput = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 128, true);
        await CopyWithProgressAsync(httpInput, httpOutput, displayPath, fileIndex, totalFiles, completedBytesBeforeFile, totalBytes, progress, cancellationToken);
    }

    private static async Task CopyWithProgressAsync(
        Stream input,
        Stream output,
        string displayPath,
        int fileIndex,
        int totalFiles,
        long completedBytesBeforeFile,
        long totalBytes,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[1024 * 128];
        long copiedBytes = 0;
        var currentFileTotalBytes = input.CanSeek ? input.Length : 0;
        int read;
        while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            copiedBytes += read;
            progress?.Report(new DownloadProgress
            {
                CompletedFiles = fileIndex,
                TotalFiles = totalFiles,
                CompletedBytes = completedBytesBeforeFile + copiedBytes,
                TotalBytes = totalBytes,
                CurrentFileCompletedBytes = copiedBytes,
                CurrentFileTotalBytes = currentFileTotalBytes,
                CurrentPath = displayPath
            });
        }
    }

    private static long GetTransferSize(ManifestFile file)
    {
        return file.Compressed && file.CompressedSize > 0
            ? file.CompressedSize
            : file.Size;
    }

    private static bool TryGetLocalSourcePath(string source, out string localPath)
    {
        localPath = string.Empty;

        if (Uri.TryCreate(source, UriKind.Absolute, out var uri) && uri.IsFile)
        {
            localPath = uri.LocalPath;
            return true;
        }

        if (Path.IsPathFullyQualified(source))
        {
            localPath = source;
            return true;
        }

        return false;
    }

    private static async Task CopyLocalFileAsync(
        FileVerificationResult item,
        string localSourcePath,
        string tempPath,
        int fileIndex,
        int totalFiles,
        long completedBytesBeforeFile,
        long totalBytes,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(localSourcePath))
        {
            throw new FileNotFoundException($"Patch source file not found: {localSourcePath}", localSourcePath);
        }

        if (File.Exists(tempPath))
        {
            File.Delete(tempPath);
        }

        await using (var input = new FileStream(localSourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 128, true))
        await using (var output = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 128, true))
        {
            var buffer = new byte[1024 * 128];
            long copiedBytes = 0;
            int read;
            while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                copiedBytes += read;
                progress?.Report(new DownloadProgress
                {
                    CompletedFiles = fileIndex,
                    TotalFiles = totalFiles,
                    CompletedBytes = completedBytesBeforeFile + copiedBytes,
                    TotalBytes = totalBytes,
                    CurrentFileCompletedBytes = copiedBytes,
                    CurrentFileTotalBytes = item.File.Size,
                    CurrentPath = item.File.Path
                });
            }
        }

        var actualSha256 = await FileHasher.ComputeSha256Async(tempPath, cancellationToken);
        if (!string.Equals(actualSha256, item.File.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Copied file failed SHA256 verification: {item.File.Path}");
        }
    }
}
