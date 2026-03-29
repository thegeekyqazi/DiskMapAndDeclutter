namespace StorageVisualizer.Protocol;

public sealed class AgentRequest
{
    public string RequestId { get; init; } = Guid.NewGuid().ToString("N");

    public string ClientName { get; init; } = "unknown";

    public string AuthToken { get; init; } = string.Empty;

    public AgentCommandType Command { get; init; }

    public string? TargetPath { get; init; }

    public AgentTransportKind Transport { get; init; } = AgentTransportKind.Unknown;
}
