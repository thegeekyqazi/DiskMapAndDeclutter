namespace StorageVisualizer.App.Models;

public sealed class SunburstNodeDetail
{
    public bool IsProtected { get; init; }

    public string ProtectionReason { get; init; } = string.Empty;

    public int DirectFileCount { get; init; }

    public int DirectChildDirectoryCount { get; init; }

    public int AccessIssueCount { get; init; }

    public int ReparsePointSkipCount { get; init; }

    public int SkippedDirectoryCount { get; init; }

    public int SuspectedLockedFileCount { get; init; }

    public int DirectArchiveFileCount { get; init; }

    public int DirectInstallerFileCount { get; init; }

    public int DirectLogFileCount { get; init; }

    public int LargeDirectFileCount { get; init; }

    public DateTimeOffset? LatestContentWriteUtc { get; init; }
}
