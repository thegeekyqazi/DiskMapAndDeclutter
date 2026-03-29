namespace StorageVisualizer.App.Models;

public sealed class SunburstResponse
{
    public List<string> Ids { get; } = [];

    public List<string> Labels { get; } = [];

    public List<string> Parents { get; } = [];

    public List<long> Values { get; } = [];

    public List<SunburstNodeDetail> NodeDetails { get; } = [];

    public IReadOnlyList<CleanupRecommendation> Recommendations { get; init; } = [];

    public ScanSummary Summary { get; init; } = new();
}
