using Microsoft.Extensions.Options;
using StorageVisualizer.App.Configuration;

namespace StorageVisualizer.App.Services;

public sealed class ScanPathGuard
{
    private readonly StorageScanOptions _options;

    public ScanPathGuard(IOptions<StorageScanOptions> options)
    {
        _options = options.Value;
    }

    public DirectoryInfo GetValidatedRootDirectory(string rootPath)
    {
        var normalizedPath = Path.GetFullPath(rootPath.Trim());
        var rootDirectory = new DirectoryInfo(normalizedPath);

        if (!rootDirectory.Exists)
        {
            throw new DirectoryNotFoundException(
                $"The directory '{normalizedPath}' does not exist.");
        }

        EnsureSafeTarget(rootDirectory);
        return rootDirectory;
    }

    public string NormalizeAndValidateChildPath(string rootPath, string candidatePath)
    {
        var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath.Trim()));
        var normalizedCandidate = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidatePath.Trim()));

        if (!IsWithinPath(normalizedCandidate, normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"The path '{normalizedCandidate}' is outside the current scan root.");
        }

        return normalizedCandidate;
    }

    private void EnsureSafeTarget(DirectoryInfo directory)
    {
        if (IsReparsePoint(directory))
        {
            throw new ScanSafetyException(
                "Scanning a reparse-point root is blocked. Pick a normal directory instead.");
        }

        if (OperatingSystem.IsWindows())
        {
            foreach (var blockedPrefix in GetBlockedWindowsPrefixes())
            {
                if (IsWithinPath(directory.FullName, blockedPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ScanSafetyException(
                        $"Scanning '{blockedPrefix}' is blocked by the safety guardrail.");
                }
            }
        }

        if (OperatingSystem.IsLinux() &&
            (directory.FullName == "/" ||
             IsWithinPath(directory.FullName, "/boot", StringComparison.Ordinal)))
        {
            throw new ScanSafetyException(
                "Scanning '/' and '/boot' is blocked by the safety guardrail.");
        }
    }

    private IEnumerable<string> GetBlockedWindowsPrefixes()
    {
        foreach (var configuredPrefix in _options.BlockedPathPrefixes)
        {
            if (!string.IsNullOrWhiteSpace(configuredPrefix))
            {
                yield return Path.GetFullPath(configuredPrefix);
            }
        }

        var windowsDirectory = Environment.GetEnvironmentVariable("WINDIR");
        if (!string.IsNullOrWhiteSpace(windowsDirectory))
        {
            yield return Path.GetFullPath(windowsDirectory);
        }
    }

    private static bool IsReparsePoint(FileSystemInfo entry)
    {
        return entry.Attributes.HasFlag(FileAttributes.ReparsePoint);
    }

    private static bool IsWithinPath(string candidatePath, string blockedPath, StringComparison comparison)
    {
        var normalizedCandidate = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidatePath));
        var normalizedBlocked = Path.TrimEndingDirectorySeparator(Path.GetFullPath(blockedPath));

        return normalizedCandidate.Equals(normalizedBlocked, comparison) ||
               normalizedCandidate.StartsWith(
                   normalizedBlocked + Path.DirectorySeparatorChar,
                   comparison);
    }
}
