namespace L2ModernUpdater.Core;

public sealed class ExtraFileResult
{
    public required string RelativePath { get; init; }

    public required string LocalPath { get; init; }

    public required long Size { get; init; }
}
