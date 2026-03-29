namespace StorageVisualizer.App.Models;

public sealed class ReviewEntry
{
    public string Path { get; init; } = string.Empty;

    public string Decision { get; init; } = "none";

    public bool IsReviewed { get; init; }

    public bool IsHidden { get; init; }

    public bool IsFavorite { get; init; }

    public string Note { get; init; } = string.Empty;

    public DateTimeOffset UpdatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
