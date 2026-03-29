using Microsoft.Win32;

namespace StorageVisualizer.App.Services;

public sealed class InstalledProgramCatalog
{
    private const string UninstallRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";

    private readonly Lazy<IReadOnlyList<InstalledProgramLocation>> _locations;

    public InstalledProgramCatalog()
    {
        _locations = new Lazy<IReadOnlyList<InstalledProgramLocation>>(LoadLocations);
    }

    public IReadOnlyList<InstalledProgramLocation> GetLocations()
    {
        return _locations.Value;
    }

    public bool TryMatch(string candidatePath, out InstalledProgramLocation match)
    {
        var normalizedCandidate = NormalizePath(candidatePath);

        foreach (var location in GetLocations())
        {
            if (IsWithinPath(normalizedCandidate, location.Path))
            {
                match = location;
                return true;
            }
        }

        match = null!;
        return false;
    }

    private static IReadOnlyList<InstalledProgramLocation> LoadLocations()
    {
        if (!OperatingSystem.IsWindows())
        {
            return [];
        }

        var registryViews = new (RegistryHive Hive, RegistryView View)[]
        {
            (RegistryHive.LocalMachine, RegistryView.Registry64),
            (RegistryHive.LocalMachine, RegistryView.Registry32),
            (RegistryHive.CurrentUser, RegistryView.Registry64),
            (RegistryHive.CurrentUser, RegistryView.Registry32)
        };

        var deduped = new Dictionary<string, InstalledProgramLocation>(StringComparer.OrdinalIgnoreCase);

        foreach (var (hive, view) in registryViews)
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                using var uninstallKey = baseKey.OpenSubKey(UninstallRegistryPath);
                if (uninstallKey is null)
                {
                    continue;
                }

                foreach (var subKeyName in uninstallKey.GetSubKeyNames())
                {
                    try
                    {
                        using var appKey = uninstallKey.OpenSubKey(subKeyName);
                        if (appKey is null)
                        {
                            continue;
                        }

                        var installLocation = appKey.GetValue("InstallLocation") as string;
                        if (string.IsNullOrWhiteSpace(installLocation))
                        {
                            continue;
                        }

                        var normalizedPath = NormalizePath(installLocation);
                        if (!Directory.Exists(normalizedPath))
                        {
                            continue;
                        }

                        if (deduped.ContainsKey(normalizedPath))
                        {
                            continue;
                        }

                        var displayName = appKey.GetValue("DisplayName") as string;
                        if (string.IsNullOrWhiteSpace(displayName))
                        {
                            displayName = Path.GetFileName(normalizedPath);
                        }

                        deduped[normalizedPath] = new InstalledProgramLocation(displayName!, normalizedPath);
                    }
                    catch (IOException)
                    {
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return deduped.Values
            .OrderByDescending(location => location.Path.Length)
            .ToArray();
    }

    private static bool IsWithinPath(string candidatePath, string blockedPath)
    {
        return candidatePath.Equals(blockedPath, StringComparison.OrdinalIgnoreCase) ||
               candidatePath.StartsWith(blockedPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string path)
    {
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }
}

public sealed record InstalledProgramLocation(string DisplayName, string Path);
