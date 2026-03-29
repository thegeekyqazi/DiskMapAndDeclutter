namespace StorageVisualizer.App.Models;

public sealed class DuplicateFileAnalysisResponse
{
    public string RootPath { get; init; } = string.Empty;

    public DateTimeOffset GeneratedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public int CandidateFileCount { get; init; }

    public int GroupCount { get; init; }

    public long EstimatedDuplicateBytes { get; init; }

    public IReadOnlyList<string> Warnings { get; init; } = [];

    public IReadOnlyList<DuplicateFileGroup> Groups { get; init; } = [];
}
