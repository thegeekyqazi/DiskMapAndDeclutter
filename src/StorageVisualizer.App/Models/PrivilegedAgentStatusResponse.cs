using StorageVisualizer.Protocol;

namespace StorageVisualizer.App.Models;

public sealed class PrivilegedAgentStatusResponse
{
    public bool IsReachable { get; init; }

    public string Message { get; init; } = string.Empty;

    public string Transport { get; init; } = "unavailable";

    public string ConfiguredTransportMode { get; init; } = AgentTransportMode.Auto.ToString();

    public AgentStatus? Status { get; init; }
}
