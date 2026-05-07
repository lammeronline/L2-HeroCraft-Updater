namespace L2ModernUpdater.Core;

public sealed class VerificationProgress
{
    public required int Completed { get; init; }

    public required int Total { get; init; }

    public required string CurrentPath { get; init; }
}
