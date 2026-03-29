namespace StorageVisualizer.App.Configuration;

public sealed class StorageScanOptions
{
    public const string SectionName = "StorageScan";

    public long MinimumChildBytes { get; init; } = 1_048_576;

    public int LockCheckLimit { get; init; } = 200;

    public long RecommendationMinimumBytes { get; init; } = 134_217_728;

    public long HighPriorityRecommendationBytes { get; init; } = 536_870_912;

    public long LargeFlatFolderBytes { get; init; } = 268_435_456;

    public long LargeDirectFileBytes { get; init; } = 104_857_600;

    public int FlatFolderDirectFileThreshold { get; init; } = 30;

    public int MaxRecommendations { get; init; } = 8;

    public int StaleFolderDays { get; init; } = 180;

    public string[] SkippedDirectoryNames { get; init; } =
    [
        ".git",
        "node_modules",
        "__pycache__"
    ];

    public string[] CacheLikeDirectoryNames { get; init; } =
    [
        "temp",
        "tmp",
        "cache",
        "caches",
        "logs",
        "crashdumps",
        "thumbnails"
    ];

    public string[] ArchiveFileExtensions { get; init; } =
    [
        ".zip",
        ".rar",
        ".7z",
        ".tar",
        ".gz",
        ".bz2"
    ];

    public string[] InstallerFileExtensions { get; init; } =
    [
        ".msi",
        ".exe",
        ".msix",
        ".appx",
        ".pkg"
    ];

    public string[] BlockedPathPrefixes { get; init; } =
    [
        @"C:\Windows"
    ];
}
