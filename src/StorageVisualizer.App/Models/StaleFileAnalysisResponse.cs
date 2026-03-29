namespace StorageVisualizer.App.Models;

public sealed class StaleFileAnalysisResponse
{
    public string RootPath { get; init; } = string.Empty;

    public DateTimeOffset GeneratedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public int ResultCount { get; init; }

    public IReadOnlyList<string> Warnings { get; init; } = [];

    public IReadOnlyList<StaleFileItem> Files { get; init; } = [];
}
