using StorageVisualizer.App.Models;

namespace StorageVisualizer.App.Services;

public sealed class SunburstFlattener
{
    public SunburstResponse Flatten(StorageScanResult result)
    {
        var response = new SunburstResponse
        {
            Summary = result.Summary,
            Recommendations = result.Recommendations
        };
        Traverse(result.RootNode, string.Empty, response);

        if (response.Parents.Count > 0)
        {
            response.Parents[0] = string.Empty;
        }

        result.Summary.DisplayedNodeCount = response.Ids.Count;
        result.Summary.RecommendationCount = response.Recommendations.Count;

        return response;
    }

    private static void Traverse(StorageNode node, string parentId, SunburstResponse response)
    {
        response.Ids.Add(node.Path);
        response.Labels.Add(node.Name);
        response.Parents.Add(parentId);
        response.Values.Add(node.Size);
        response.NodeDetails.Add(new SunburstNodeDetail
        {
            IsProtected = node.Metadata.IsProtected,
            ProtectionReason = node.Metadata.ProtectionReason ?? string.Empty,
            DirectFileCount = node.Metadata.DirectFileCount,
            DirectChildDirectoryCount = node.Metadata.DirectChildDirectoryCount,
            AccessIssueCount = node.Metadata.AccessIssueCount,
            ReparsePointSkipCount = node.Metadata.ReparsePointSkipCount,
            SkippedDirectoryCount = node.Metadata.SkippedDirectoryCount,
            SuspectedLockedFileCount = node.Metadata.SuspectedLockedFileCount,
            DirectArchiveFileCount = node.Metadata.DirectArchiveFileCount,
            DirectInstallerFileCount = node.Metadata.DirectInstallerFileCount,
            DirectLogFileCount = node.Metadata.DirectLogFileCount,
            LargeDirectFileCount = node.Metadata.LargeDirectFileCount,
            LatestContentWriteUtc = node.Metadata.LatestContentWriteUtc
        });

        foreach (var child in node.Children)
        {
            Traverse(child, node.Path, response);
        }
    }
}
