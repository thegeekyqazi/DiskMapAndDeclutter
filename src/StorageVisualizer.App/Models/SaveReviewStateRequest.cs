namespace StorageVisualizer.App.Models;

public sealed class SaveReviewStateRequest
{
    public string RootPath { get; init; } = string.Empty;

    public IReadOnlyList<ReviewEntry> Entries { get; init; } = [];
}
