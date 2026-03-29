namespace StorageVisualizer.App.Models;

public sealed class CleanupRecommendation
{
    public string Category { get; init; } = string.Empty;

    public string Priority { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Path { get; init; } = string.Empty;

    public string Reason { get; init; } = string.Empty;

    public long EstimatedBytes { get; init; }

    public string[] Signals { get; init; } = [];
}
