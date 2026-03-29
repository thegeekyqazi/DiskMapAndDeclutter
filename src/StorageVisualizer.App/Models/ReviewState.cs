namespace StorageVisualizer.App.Models;

public sealed class ReviewState
{
    public string RootPath { get; init; } = string.Empty;

    public DateTimeOffset SavedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public IReadOnlyList<ReviewEntry> Entries { get; init; } = [];
}
