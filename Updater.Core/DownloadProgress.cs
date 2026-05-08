namespace L2ModernUpdater.Core;

public sealed class DownloadProgress
{
    public required int CompletedFiles { get; init; }

    public required int TotalFiles { get; init; }

    public required long CompletedBytes { get; init; }

    public required long TotalBytes { get; init; }

    public required long CurrentFileCompletedBytes { get; init; }

    public required long CurrentFileTotalBytes { get; init; }

    public required string CurrentPath { get; init; }
}
