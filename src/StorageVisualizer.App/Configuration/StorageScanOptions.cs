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

    public int StaleFileDays { get; init; } = 180;

    public long StaleFileMinimumBytes { get; init; } = 5_242_880;

    public int MaxStaleFiles { get; init; } = 40;

    public long DuplicateMinimumBytes { get; init; } = 1_048_576;

    public int DuplicateHashBytes { get; init; } = 131_072;

    public int MaxDuplicateCandidateFiles { get; init; } = 2000;

    public int MaxDuplicateGroups { get; init; } = 20;

    public int MaxDuplicateFilesPerGroup { get; init; } = 8;

    public int InspectionPreviewLineLimit { get; init; } = 20;

    public int InspectionZipEntryLimit { get; init; } = 24;

    public int InspectionHexByteCount { get; init; } = 64;

    public int MaxInspectionPreviewBytes { get; init; } = 131_072;

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

    public string[] TextPreviewExtensions { get; init; } =
    [
        ".txt",
        ".md",
        ".log",
        ".json",
        ".jsonl",
        ".xml",
        ".csv",
        ".tsv",
        ".yml",
        ".yaml",
        ".ini",
        ".config",
        ".toml",
        ".ps1",
        ".bat",
        ".cmd",
        ".cs",
        ".py",
        ".js",
        ".ts",
        ".tsx",
        ".jsx",
        ".html",
        ".css",
        ".scss",
        ".sql"
    ];

    public string[] BlockedPathPrefixes { get; init; } =
    [
        @"C:\Windows"
    ];
}
