using StorageVisualizer.Protocol;

namespace StorageVisualizer.Agent.Services;

public sealed class AgentOptions
{
    public string PipeName { get; init; } = PipeConstants.DefaultPipeName;

    public string ClientToken { get; init; } = "local-dev-agent-token";

    public string AuditLogPath { get; init; } = Path.Combine(
        Directory.GetCurrentDirectory(),
        "logs",
        "agent-audit.log");

    public bool CurrentUserOnly { get; init; }

    public bool DestructiveOperationsEnabled { get; init; } = false;

    public bool RunningAsService { get; init; }

    public string AgentMode { get; init; } = "development";

    public string TransportMode { get; init; } = AgentTransportMode.Auto.ToString();

    public int LoopbackPort { get; init; } = 5091;

    public DateTimeOffset StartedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public static AgentOptions LoadFromCurrentDirectory()
    {
        var path = Path.Combine(
            Directory.GetCurrentDirectory(),
            "src",
            "StorageVisualizer.Agent",
            "agentsettings.json");

        if (!File.Exists(path))
        {
            return new AgentOptions();
        }

        var json = File.ReadAllText(path);
        return System.Text.Json.JsonSerializer.Deserialize<AgentOptions>(json) ?? new AgentOptions();
    }
}
