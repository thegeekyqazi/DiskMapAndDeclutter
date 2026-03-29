using StorageVisualizer.Protocol;

namespace StorageVisualizer.App.Configuration;

public sealed class PrivilegedAgentOptions
{
    public const string SectionName = "PrivilegedAgent";

    public string PipeName { get; init; } = PipeConstants.DefaultPipeName;

    public string ClientToken { get; init; } = "local-dev-agent-token";

    public int TimeoutMilliseconds { get; init; } = 1500;

    public string TransportMode { get; init; } = AgentTransportMode.Auto.ToString();

    public int LoopbackPort { get; init; } = 5091;
}
