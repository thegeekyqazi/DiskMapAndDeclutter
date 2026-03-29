namespace StorageVisualizer.App.Models;

public sealed class StorageNodeMetadata
{
    public bool IsProtected { get; set; }

    public string? ProtectionReason { get; set; }

    public int DirectFileCount { get; set; }

    public int DirectChildDirectoryCount { get; set; }

    public int AccessIssueCount { get; set; }

    public int ReparsePointSkipCount { get; set; }

    public int SkippedDirectoryCount { get; set; }

    public int SuspectedLockedFileCount { get; set; }

    public int DirectArchiveFileCount { get; set; }

    public int DirectInstallerFileCount { get; set; }

    public int DirectLogFileCount { get; set; }

    public int LargeDirectFileCount { get; set; }

    public DateTimeOffset? LatestContentWriteUtc { get; set; }
}
