namespace StorageVisualizer.App.Models;

public sealed class StorageScanResult
{
    public required StorageNode RootNode { get; init; }

    public required ScanSummary Summary { get; init; }

    public IReadOnlyList<CleanupRecommendation> Recommendations { get; init; } = [];
}
