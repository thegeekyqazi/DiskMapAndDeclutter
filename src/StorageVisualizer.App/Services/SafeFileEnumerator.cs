using Microsoft.Extensions.Options;
using StorageVisualizer.App.Configuration;

namespace StorageVisualizer.App.Services;

public sealed class SafeFileEnumerator
{
    private readonly ScanPathGuard _pathGuard;
    private readonly HashSet<string> _skippedDirectoryNames;

    public SafeFileEnumerator(IOptions<StorageScanOptions> options, ScanPathGuard pathGuard)
    {
        _pathGuard = pathGuard;
        _skippedDirectoryNames = new HashSet<string>(
            options.Value.SkippedDirectoryNames,
            StringComparer.OrdinalIgnoreCase);
    }

    public DirectoryInfo GetValidatedRootDirectory(string rootPath)
    {
        return _pathGuard.GetValidatedRootDirectory(rootPath);
    }

    public string NormalizeAndValidateChildPath(string rootPath, string candidatePath)
    {
        return _pathGuard.NormalizeAndValidateChildPath(rootPath, candidatePath);
    }

    public IEnumerable<FileInfo> EnumerateFiles(string rootPath)
    {
        return EnumerateFiles(_pathGuard.GetValidatedRootDirectory(rootPath));
    }

    public IEnumerable<FileInfo> EnumerateFiles(DirectoryInfo rootDirectory)
    {
        return EnumerateFilesCore(rootDirectory);
    }

    private IEnumerable<FileInfo> EnumerateFilesCore(DirectoryInfo directory)
    {
        IEnumerable<FileSystemInfo> entries;
        try
        {
            entries = directory.EnumerateFileSystemInfos();
        }
        catch (IOException)
        {
            yield break;
        }
        catch (UnauthorizedAccessException)
        {
            yield break;
        }

        foreach (var entry in entries)
        {
            if (entry.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                continue;
            }

            if (entry is FileInfo file)
            {
                yield return file;
                continue;
            }

            if (entry is DirectoryInfo childDirectory)
            {
                if (_skippedDirectoryNames.Contains(childDirectory.Name))
                {
                    continue;
                }

                foreach (var childFile in EnumerateFilesCore(childDirectory))
                {
                    yield return childFile;
                }
            }
        }
    }
}
