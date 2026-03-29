using Microsoft.Extensions.Options;
using StorageVisualizer.App.Configuration;
using StorageVisualizer.App.Models;

namespace StorageVisualizer.App.Services;

public sealed class DirectoryScanner
{
    private readonly StorageScanOptions _options;
    private readonly HashSet<string> _skippedDirectoryNames;
    private readonly HashSet<string> _archiveExtensions;
    private readonly HashSet<string> _installerExtensions;
    private readonly ScanPathGuard _pathGuard;
    private readonly InstalledProgramCatalog _installedProgramCatalog;
    private readonly FileLockInspector _fileLockInspector;

    public DirectoryScanner(
        IOptions<StorageScanOptions> options,
        ScanPathGuard pathGuard,
        InstalledProgramCatalog installedProgramCatalog,
        FileLockInspector fileLockInspector)
    {
        _options = options.Value;
        _pathGuard = pathGuard;
        _installedProgramCatalog = installedProgramCatalog;
        _fileLockInspector = fileLockInspector;
        _skippedDirectoryNames = new HashSet<string>(
            _options.SkippedDirectoryNames,
            StringComparer.OrdinalIgnoreCase);
        _archiveExtensions = new HashSet<string>(
            _options.ArchiveFileExtensions,
            StringComparer.OrdinalIgnoreCase);
        _installerExtensions = new HashSet<string>(
            _options.InstallerFileExtensions,
            StringComparer.OrdinalIgnoreCase);
    }

    public StorageScanResult BuildStorageTree(string rootPath)
    {
        var rootDirectory = _pathGuard.GetValidatedRootDirectory(rootPath);

        var summary = new ScanSummary
        {
            RootPath = rootDirectory.FullName,
            ScannedAtUtc = DateTimeOffset.UtcNow,
            InstalledProgramLocationCount = _installedProgramCatalog.GetLocations().Count
        };

        var context = new ScanContext(_options.LockCheckLimit, summary);
        var rootNode = ScanDirectory(rootDirectory, context);

        AppendWarnings(summary);

        return new StorageScanResult
        {
            RootNode = rootNode,
            Summary = summary
        };
    }

    private StorageNode ScanDirectory(DirectoryInfo directory, ScanContext context)
    {
        context.Summary.TotalDirectoriesSeen++;

        var node = new StorageNode(GetDisplayName(directory), directory.FullName);
        ApplyProtectionMetadata(node, context);

        IEnumerable<FileSystemInfo> entries;
        try
        {
            entries = directory.EnumerateFileSystemInfos();
        }
        catch (UnauthorizedAccessException)
        {
            RegisterAccessIssue(node, context);
            return node;
        }
        catch (IOException)
        {
            RegisterAccessIssue(node, context);
            return node;
        }

        foreach (var entry in entries)
        {
            try
            {
                if (IsReparsePoint(entry))
                {
                    node.Metadata.ReparsePointSkipCount++;
                    context.Summary.ReparsePointSkipCount++;
                    continue;
                }

                if (entry is FileInfo file)
                {
                    node.Metadata.DirectFileCount++;
                    context.Summary.TotalFilesSeen++;
                    node.Size += file.Length;
                    UpdateClassificationMetadata(file, node);
                    UpdateLatestWrite(node, file.LastWriteTimeUtc);

                    if (context.RemainingLockChecks > 0 && _fileLockInspector.IsProbablyLocked(file.FullName))
                    {
                        node.Metadata.SuspectedLockedFileCount++;
                        context.Summary.SuspectedLockedFileCount++;
                    }

                    if (context.RemainingLockChecks > 0)
                    {
                        context.RemainingLockChecks--;
                        context.Summary.LockChecksPerformed++;
                    }

                    continue;
                }

                if (entry is DirectoryInfo childDirectory)
                {
                    if (_skippedDirectoryNames.Contains(childDirectory.Name))
                    {
                        node.Metadata.SkippedDirectoryCount++;
                        context.Summary.SkippedDirectoryCount++;
                        continue;
                    }

                    node.Metadata.DirectChildDirectoryCount++;

                    var childNode = ScanDirectory(childDirectory, context);
                    node.Size += childNode.Size;
                    UpdateLatestWrite(node, childNode.Metadata.LatestContentWriteUtc);

                    if (childNode.Size > _options.MinimumChildBytes)
                    {
                        node.Children.Add(childNode);
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                RegisterAccessIssue(node, context);
            }
            catch (IOException)
            {
                RegisterAccessIssue(node, context);
            }
        }

        return node;
    }

    private void ApplyProtectionMetadata(StorageNode node, ScanContext context)
    {
        if (_installedProgramCatalog.TryMatch(node.Path, out var program))
        {
            node.Metadata.IsProtected = true;
            node.Metadata.ProtectionReason = $"Matches installed application path: {program.DisplayName}";
            context.Summary.ProtectedNodeCount++;
        }
    }

    private static void RegisterAccessIssue(StorageNode node, ScanContext context)
    {
        node.Metadata.AccessIssueCount++;
        context.Summary.AccessIssueCount++;
    }

    private void UpdateClassificationMetadata(FileInfo file, StorageNode node)
    {
        var extension = file.Extension;
        if (_archiveExtensions.Contains(extension))
        {
            node.Metadata.DirectArchiveFileCount++;
        }

        if (_installerExtensions.Contains(extension))
        {
            node.Metadata.DirectInstallerFileCount++;
        }

        if (string.Equals(extension, ".log", StringComparison.OrdinalIgnoreCase))
        {
            node.Metadata.DirectLogFileCount++;
        }

        if (file.Length >= _options.LargeDirectFileBytes)
        {
            node.Metadata.LargeDirectFileCount++;
        }
    }

    private static void UpdateLatestWrite(StorageNode node, DateTime latestWriteUtc)
    {
        UpdateLatestWrite(node, new DateTimeOffset(DateTime.SpecifyKind(latestWriteUtc, DateTimeKind.Utc)));
    }

    private static void UpdateLatestWrite(StorageNode node, DateTimeOffset? latestWriteUtc)
    {
        if (latestWriteUtc is null)
        {
            return;
        }

        if (node.Metadata.LatestContentWriteUtc is null || latestWriteUtc > node.Metadata.LatestContentWriteUtc)
        {
            node.Metadata.LatestContentWriteUtc = latestWriteUtc;
        }
    }

    private static void AppendWarnings(ScanSummary summary)
    {
        if (summary.InstalledProgramLocationCount > 0)
        {
            summary.Warnings.Add(
                $"{summary.InstalledProgramLocationCount} installed-program locations were loaded from the Windows uninstall registry.");
        }

        if (summary.ProtectedNodeCount > 0)
        {
            summary.Warnings.Add(
                $"{summary.ProtectedNodeCount} displayed folders overlap installed-program paths and should be treated as protected.");
        }

        if (summary.SuspectedLockedFileCount > 0)
        {
            summary.Warnings.Add(
                $"{summary.SuspectedLockedFileCount} files looked in use during best-effort lock checks.");
        }

        if (summary.AccessIssueCount > 0)
        {
            summary.Warnings.Add(
                $"{summary.AccessIssueCount} items could not be fully read because of permissions or transient I/O errors.");
        }

        if (summary.ReparsePointSkipCount > 0)
        {
            summary.Warnings.Add(
                $"{summary.ReparsePointSkipCount} reparse points were skipped to avoid following symlinks or junctions.");
        }
    }
    private static bool IsReparsePoint(FileSystemInfo entry)
    {
        return entry.Attributes.HasFlag(FileAttributes.ReparsePoint);
    }

    private static string GetDisplayName(DirectoryInfo directory)
    {
        return string.IsNullOrWhiteSpace(directory.Name)
            ? directory.FullName
            : directory.Name;
    }

    private sealed class ScanContext
    {
        public ScanContext(int remainingLockChecks, ScanSummary summary)
        {
            RemainingLockChecks = remainingLockChecks;
            Summary = summary;
        }

        public int RemainingLockChecks { get; set; }

        public ScanSummary Summary { get; }
    }
}
