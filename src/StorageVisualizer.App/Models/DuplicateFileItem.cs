namespace StorageVisualizer.App.Models;

public sealed class DuplicateFileItem
{
    public string Path { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string DirectoryPath { get; init; } = string.Empty;

    public long SizeBytes { get; init; }

    public DateTimeOffset LastWriteUtc { get; init; }

    public DateTimeOffset LastAccessUtc { get; init; }

    public bool IsProtected { get; init; }

    public string ProtectionReason { get; init; } = string.Empty;

    public bool IsProbablyLocked { get; init; }
}
