namespace L2ModernUpdater.Core;

public sealed class FileVerificationResult
{
    public required ManifestFile File { get; init; }

    public required string LocalPath { get; init; }

    public required VerificationState State { get; init; }

    public string? ActualSha256 { get; init; }
}
