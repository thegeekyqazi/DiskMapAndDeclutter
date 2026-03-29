using Microsoft.Extensions.Options;
using StorageVisualizer.App.Configuration;
using StorageVisualizer.App.Models;

namespace StorageVisualizer.App.Services;

public sealed class StaleFileAnalysisService
{
    private readonly StorageScanOptions _options;
    private readonly SafeFileEnumerator _fileEnumerator;
    private readonly InstalledProgramCatalog _installedProgramCatalog;
    private readonly FileLockInspector _fileLockInspector;

    public StaleFileAnalysisService(
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

    public Task<StaleFileAnalysisResponse> AnalyzeAsync(string rootPath, CancellationToken cancellationToken)
    {
        var rootDirectory = _fileEnumerator.GetValidatedRootDirectory(rootPath);
        var cutoff = DateTimeOffset.UtcNow.AddDays(-_options.StaleFileDays);
        var warnings = new List<string>
        {
            "Stale-file analysis is read-only. Windows last-access timestamps can be approximate on some systems."
        };

        var files = new List<StaleFileItem>();
        foreach (var file in _fileEnumerator.EnumerateFiles(rootDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (file.Length < _options.StaleFileMinimumBytes)
            {
                continue;
            }

            var lastWriteUtc = ToUtcOffset(file.LastWriteTimeUtc);
            var lastAccessUtc = ToUtcOffset(file.LastAccessTimeUtc);
            var lastActivityUtc = lastAccessUtc > lastWriteUtc ? lastAccessUtc : lastWriteUtc;

            if (lastActivityUtc > cutoff)
            {
                continue;
            }

            var isProtected = _installedProgramCatalog.TryMatch(file.FullName, out var program);
            files.Add(new StaleFileItem
            {
                Path = file.FullName,
                Name = file.Name,
                DirectoryPath = file.DirectoryName ?? string.Empty,
                Extension = file.Extension,
                SizeBytes = file.Length,
                LastActivityUtc = lastActivityUtc,
                LastWriteUtc = lastWriteUtc,
                LastAccessUtc = lastAccessUtc,
                IsProtected = isProtected,
                ProtectionReason = isProtected ? $"Matches installed application path: {program.DisplayName}" : string.Empty,
                IsProbablyLocked = _fileLockInspector.IsProbablyLocked(file.FullName)
            });
        }

        var orderedFiles = files
            .OrderBy(item => item.LastActivityUtc)
            .ThenByDescending(item => item.SizeBytes)
            .Take(_options.MaxStaleFiles)
            .ToArray();

        if (files.Count > orderedFiles.Length)
        {
            warnings.Add(
                $"Only the top {_options.MaxStaleFiles} stale-file candidates are shown. Narrow the scan root if you want a shorter list.");
        }

        if (orderedFiles.Length == 0)
        {
            warnings.Add("No stale-file candidates met the current age and size thresholds.");
        }

        return Task.FromResult(new StaleFileAnalysisResponse
        {
            RootPath = rootDirectory.FullName,
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            ResultCount = orderedFiles.Length,
            Warnings = warnings,
            Files = orderedFiles
        });
    }

    private static DateTimeOffset ToUtcOffset(DateTime value)
    {
        return new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    }
}
