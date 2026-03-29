namespace StorageVisualizer.App.Models;

public sealed class FileInspectionResponse
{
    public string RootPath { get; init; } = string.Empty;

    public string TargetPath { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Extension { get; init; } = string.Empty;

    public long SizeBytes { get; init; }

    public DateTimeOffset CreatedUtc { get; init; }

    public DateTimeOffset LastWriteUtc { get; init; }

    public DateTimeOffset LastAccessUtc { get; init; }

    public string Attributes { get; init; } = string.Empty;

    public bool IsProtected { get; init; }

    public string ProtectionReason { get; init; } = string.Empty;

    public bool IsProbablyLocked { get; init; }

    public string PreviewKind { get; init; } = "summary";

    public string PreviewTitle { get; init; } = string.Empty;

    public IReadOnlyList<string> PreviewLines { get; init; } = [];

    public string Summary { get; init; } = string.Empty;
}
