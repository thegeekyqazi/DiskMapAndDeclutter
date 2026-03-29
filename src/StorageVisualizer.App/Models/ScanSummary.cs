namespace StorageVisualizer.App.Models;

public sealed class ScanSummary
{
    public string RootPath { get; set; } = string.Empty;

    public DateTimeOffset ScannedAtUtc { get; set; }

    public int TotalDirectoriesSeen { get; set; }

    public int TotalFilesSeen { get; set; }

    public int DisplayedNodeCount { get; set; }

    public int AccessIssueCount { get; set; }

    public int ReparsePointSkipCount { get; set; }

    public int SkippedDirectoryCount { get; set; }

    public int InstalledProgramLocationCount { get; set; }

    public int ProtectedNodeCount { get; set; }

    public int SuspectedLockedFileCount { get; set; }

    public int LockChecksPerformed { get; set; }

    public int RecommendationCount { get; set; }

    public List<string> Warnings { get; } = [];
}
