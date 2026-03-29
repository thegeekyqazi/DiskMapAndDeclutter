namespace StorageVisualizer.App.Models;

public sealed class DuplicateFileGroup
{
    public string GroupId { get; init; } = string.Empty;

    public long FileSizeBytes { get; init; }

    public long EstimatedDuplicateBytes { get; init; }

    public IReadOnlyList<DuplicateFileItem> Files { get; init; } = [];
}
