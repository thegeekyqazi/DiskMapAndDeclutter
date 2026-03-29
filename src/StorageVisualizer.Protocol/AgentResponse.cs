namespace StorageVisualizer.Protocol;

public sealed class AgentResponse
{
    public string RequestId { get; init; } = string.Empty;

    public bool Success { get; init; }

    public bool Allowed { get; init; }

    public string Message { get; init; } = string.Empty;

    public AgentStatus? Status { get; init; }
}
