using StorageVisualizer.App.Configuration;
using StorageVisualizer.App.Models;
using Microsoft.Extensions.Options;

namespace StorageVisualizer.App.Services;

public sealed class CleanupRecommendationEngine
{
    private readonly StorageScanOptions _options;
    private readonly HashSet<string> _cacheLikeNames;
    private readonly TimeSpan _staleThreshold;

    public CleanupRecommendationEngine(IOptions<StorageScanOptions> options)
    {
        _options = options.Value;
        _cacheLikeNames = new HashSet<string>(
            _options.CacheLikeDirectoryNames,
            StringComparer.OrdinalIgnoreCase);
        _staleThreshold = TimeSpan.FromDays(_options.StaleFolderDays);
    }

    public IReadOnlyList<CleanupRecommendation> Generate(StorageNode rootNode)
    {
        var recommendations = new List<CleanupRecommendation>();
        Traverse(rootNode, recommendations);

        return recommendations
            .OrderByDescending(Score)
            .ThenByDescending(item => item.EstimatedBytes)
            .Take(_options.MaxRecommendations)
            .ToArray();
    }

    private void Traverse(StorageNode node, List<CleanupRecommendation> recommendations)
    {
        var recommendation = BuildRecommendation(node);
        if (recommendation is not null)
        {
            recommendations.Add(recommendation);
        }

        foreach (var child in node.Children)
        {
            Traverse(child, recommendations);
        }
    }

    private CleanupRecommendation? BuildRecommendation(StorageNode node)
    {
        if (node.Metadata.IsProtected || node.Metadata.AccessIssueCount > 0 || node.Metadata.ReparsePointSkipCount > 0)
        {
            return null;
        }

        if (node.Size < _options.RecommendationMinimumBytes)
        {
            return null;
        }

        var name = Path.GetFileName(node.Path);
        if (node.Metadata.DirectLogFileCount >= 10 &&
            node.Metadata.DirectChildDirectoryCount <= 2)
        {
            return new CleanupRecommendation
            {
                Category = "Log-heavy folder",
                Priority = "review",
                Title = $"Review log-heavy folder '{name}'",
                Path = node.Path,
                Reason = "This folder contains many direct log files. Logs can be worth pruning or rotating manually, but they may still be needed for troubleshooting.",
                EstimatedBytes = node.Size,
                Signals =
                [
                    $"Direct log files: {node.Metadata.DirectLogFileCount}",
                    $"Estimated size: {node.Size} bytes",
                    $"Latest activity: {FormatLatestWrite(node.Metadata.LatestContentWriteUtc)}"
                ]
            };
        }

        if (_cacheLikeNames.Contains(name))
        {
            return new CleanupRecommendation
            {
                Category = "Cache-like data",
                Priority = node.Size >= _options.HighPriorityRecommendationBytes ? "high" : "medium",
                Title = $"Review cache-like folder '{name}'",
                Path = node.Path,
                Reason = "The folder name matches a common cache or temporary-data pattern and it is large enough to matter.",
                EstimatedBytes = node.Size,
                Signals =
                [
                    $"Folder name: {name}",
                    $"Estimated size: {node.Size} bytes",
                    "No protection or access-issue flags were raised for this node"
                ]
            };
        }

        if (node.Metadata.DirectInstallerFileCount + node.Metadata.DirectArchiveFileCount >= 3 &&
            IsDownloadsLike(node.Path))
        {
            return new CleanupRecommendation
            {
                Category = "Installer or archive stash",
                Priority = node.Size >= _options.HighPriorityRecommendationBytes ? "high" : "medium",
                Title = $"Review installer-heavy folder '{name}'",
                Path = node.Path,
                Reason = "The folder sits in a user drop zone and contains several installer or archive files, which often accumulate after one-time use.",
                EstimatedBytes = node.Size,
                Signals =
                [
                    $"Archive files: {node.Metadata.DirectArchiveFileCount}",
                    $"Installer files: {node.Metadata.DirectInstallerFileCount}",
                    $"Large direct files: {node.Metadata.LargeDirectFileCount}"
                ]
            };
        }

        if (IsDownloadsLike(node.Path))
        {
            return new CleanupRecommendation
            {
                Category = "Large user-owned folder",
                Priority = node.Size >= _options.HighPriorityRecommendationBytes ? "high" : "medium",
                Title = $"Review large download-style folder '{name}'",
                Path = node.Path,
                Reason = "Large folders inside common user-drop zones are often good manual cleanup candidates, but should be reviewed before any action.",
                EstimatedBytes = node.Size,
                Signals =
                [
                    $"Folder path suggests a user drop zone: {node.Path}",
                    $"Estimated size: {node.Size} bytes",
                    $"Direct files: {node.Metadata.DirectFileCount}"
                ]
            };
        }

        if (IsStale(node) &&
            node.Metadata.SuspectedLockedFileCount == 0 &&
            node.Metadata.LargeDirectFileCount > 0)
        {
            return new CleanupRecommendation
            {
                Category = "Stale large folder",
                Priority = "review",
                Title = $"Review stale large folder '{name}'",
                Path = node.Path,
                Reason = "The folder is large, appears inactive, and contains at least one very large direct file. That can make it a good manual archive candidate.",
                EstimatedBytes = node.Size,
                Signals =
                [
                    $"Latest activity: {FormatLatestWrite(node.Metadata.LatestContentWriteUtc)}",
                    $"Large direct files: {node.Metadata.LargeDirectFileCount}",
                    $"Estimated size: {node.Size} bytes"
                ]
            };
        }

        if (node.Metadata.SuspectedLockedFileCount == 0 &&
            node.Metadata.DirectChildDirectoryCount <= 2 &&
            node.Metadata.DirectFileCount >= _options.FlatFolderDirectFileThreshold &&
            node.Size >= _options.LargeFlatFolderBytes)
        {
            return new CleanupRecommendation
            {
                Category = "Large flat folder",
                Priority = "review",
                Title = $"Review flat folder '{name}'",
                Path = node.Path,
                Reason = "Large flat folders with many direct files are usually easier to sort, archive, or manually prune than deeply nested trees.",
                EstimatedBytes = node.Size,
                Signals =
                [
                    $"Direct files: {node.Metadata.DirectFileCount}",
                    $"Child folders: {node.Metadata.DirectChildDirectoryCount}",
                    $"Estimated size: {node.Size} bytes"
                ]
            };
        }

        return null;
    }

    private static bool IsDownloadsLike(string path)
    {
        return path.Contains($"{Path.DirectorySeparatorChar}Downloads", StringComparison.OrdinalIgnoreCase) ||
               path.Contains($"{Path.DirectorySeparatorChar}Desktop", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsStale(StorageNode node)
    {
        if (node.Metadata.LatestContentWriteUtc is null)
        {
            return false;
        }

        return DateTimeOffset.UtcNow - node.Metadata.LatestContentWriteUtc >= _staleThreshold;
    }

    private static string FormatLatestWrite(DateTimeOffset? value)
    {
        return value?.ToString("u") ?? "unknown";
    }

    private static int Score(CleanupRecommendation recommendation)
    {
        return recommendation.Priority switch
        {
            "high" => 3,
            "medium" => 2,
            _ => 1
        };
    }
}
