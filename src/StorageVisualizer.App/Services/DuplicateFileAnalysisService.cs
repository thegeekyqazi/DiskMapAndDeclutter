using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using StorageVisualizer.App.Configuration;
using StorageVisualizer.App.Models;

namespace StorageVisualizer.App.Services;

public sealed class DuplicateFileAnalysisService
{
    private readonly StorageScanOptions _options;
    private readonly SafeFileEnumerator _fileEnumerator;
    private readonly InstalledProgramCatalog _installedProgramCatalog;
    private readonly FileLockInspector _fileLockInspector;

    public DuplicateFileAnalysisService(
        IOptions<StorageScanOptions> options,
        SafeFileEnumerator fileEnumerator,
        InstalledProgramCatalog installedProgramCatalog,
        FileLockInspector fileLockInspector)
    {
        _options = options.Value;
        _fileEnumerator = fileEnumerator;
        _installedProgramCatalog = installedProgramCatalog;
        _fileLockInspector = fileLockInspector;
    }

    public async Task<DuplicateFileAnalysisResponse> AnalyzeAsync(string rootPath, CancellationToken cancellationToken)
    {
        var rootDirectory = _fileEnumerator.GetValidatedRootDirectory(rootPath);
        var warnings = new List<string>
        {
            "Duplicate detection is read-only. It compares same-size files, then hashes them to confirm likely duplicates."
        };

        var sizeGroups = _fileEnumerator
            .EnumerateFiles(rootDirectory)
            .Where(file => file.Length >= _options.DuplicateMinimumBytes)
            .GroupBy(file => file.Length)
            .Where(group => group.Count() > 1)
            .OrderByDescending(group => group.Key)
            .ToArray();

        var candidateGroups = new List<FileInfo[]>();
        var candidateFileCount = 0;
        var truncatedCandidates = false;

        foreach (var sizeGroup in sizeGroups)
        {
            var remaining = _options.MaxDuplicateCandidateFiles - candidateFileCount;
            if (remaining < 2)
            {
                truncatedCandidates = true;
                break;
            }

            var files = sizeGroup
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .Take(remaining)
                .ToArray();

            if (files.Length < sizeGroup.Count())
            {
                truncatedCandidates = true;
            }

            if (files.Length >= 2)
            {
                candidateGroups.Add(files);
                candidateFileCount += files.Length;
            }
        }

        if (truncatedCandidates)
        {
            warnings.Add(
                $"Only the largest {_options.MaxDuplicateCandidateFiles} duplicate candidates were hashed to keep the scan responsive.");
        }

        var results = new List<DuplicateFileGroup>();
        foreach (var candidateGroup in candidateGroups)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var partialGroups = new Dictionary<string, List<FileInfo>>(StringComparer.Ordinal);
            foreach (var file in candidateGroup)
            {
                var partialKey = await ComputePartialFingerprintAsync(file, cancellationToken);
                if (!partialGroups.TryGetValue(partialKey, out var list))
                {
                    list = [];
                    partialGroups[partialKey] = list;
                }

                list.Add(file);
            }

            foreach (var partialGroup in partialGroups.Values.Where(group => group.Count > 1))
            {
                var fullHashGroups = new Dictionary<string, List<FileInfo>>(StringComparer.Ordinal);
                foreach (var file in partialGroup)
                {
                    var fullHash = await ComputeFullHashAsync(file, cancellationToken);
                    if (!fullHashGroups.TryGetValue(fullHash, out var list))
                    {
                        list = [];
                        fullHashGroups[fullHash] = list;
                    }

                    list.Add(file);
                }

                foreach (var duplicateGroup in fullHashGroups.Values.Where(group => group.Count > 1))
                {
                    var orderedFiles = duplicateGroup
                        .OrderByDescending(file => file.Length)
                        .ThenBy(file => file.FullName, StringComparer.OrdinalIgnoreCase)
                        .Take(_options.MaxDuplicateFilesPerGroup)
                        .Select(ToDuplicateFileItem)
                        .ToArray();

                    results.Add(new DuplicateFileGroup
                    {
                        GroupId = Convert.ToHexString(SHA256.HashData(
                            System.Text.Encoding.UTF8.GetBytes(string.Join("|", duplicateGroup.Select(file => file.FullName))))),
                        FileSizeBytes = duplicateGroup[0].Length,
                        EstimatedDuplicateBytes = duplicateGroup[0].Length * (duplicateGroup.Count - 1L),
                        Files = orderedFiles
                    });
                }
            }
        }

        var orderedGroups = results
            .OrderByDescending(group => group.EstimatedDuplicateBytes)
            .ThenByDescending(group => group.FileSizeBytes)
            .Take(_options.MaxDuplicateGroups)
            .ToArray();

        if (results.Count > orderedGroups.Length)
        {
            warnings.Add(
                $"Only the top {_options.MaxDuplicateGroups} duplicate groups are shown. Narrow the scan root if you want more detail.");
        }

        if (orderedGroups.Length == 0)
        {
            warnings.Add("No duplicate groups were confirmed within the current analysis limits.");
        }

        return new DuplicateFileAnalysisResponse
        {
            RootPath = rootDirectory.FullName,
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            CandidateFileCount = candidateFileCount,
            GroupCount = orderedGroups.Length,
            EstimatedDuplicateBytes = orderedGroups.Sum(group => group.EstimatedDuplicateBytes),
            Warnings = warnings,
            Groups = orderedGroups
        };
    }

    private async Task<string> ComputePartialFingerprintAsync(FileInfo file, CancellationToken cancellationToken)
    {
        var buffer = new byte[Math.Min(_options.DuplicateHashBytes, (int)Math.Min(file.Length, int.MaxValue))];
        await using var stream = file.OpenRead();
        var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
        var hash = SHA256.HashData(buffer.AsSpan(0, read));
        return $"{file.Length}:{Convert.ToHexString(hash)}";
    }

    private static async Task<string> ComputeFullHashAsync(FileInfo file, CancellationToken cancellationToken)
    {
        using var sha = SHA256.Create();
        await using var stream = file.OpenRead();
        var hash = await sha.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hash);
    }

    private DuplicateFileItem ToDuplicateFileItem(FileInfo file)
    {
        var isProtected = _installedProgramCatalog.TryMatch(file.FullName, out var program);

        return new DuplicateFileItem
        {
            Path = file.FullName,
            Name = file.Name,
            DirectoryPath = file.DirectoryName ?? string.Empty,
            SizeBytes = file.Length,
            LastWriteUtc = new DateTimeOffset(DateTime.SpecifyKind(file.LastWriteTimeUtc, DateTimeKind.Utc)),
            LastAccessUtc = new DateTimeOffset(DateTime.SpecifyKind(file.LastAccessTimeUtc, DateTimeKind.Utc)),
            IsProtected = isProtected,
            ProtectionReason = isProtected ? $"Matches installed application path: {program.DisplayName}" : string.Empty,
            IsProbablyLocked = _fileLockInspector.IsProbablyLocked(file.FullName)
        };
    }
}
