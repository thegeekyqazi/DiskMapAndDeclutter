namespace StorageVisualizer.Protocol;

public sealed class AgentStatus
{
    public string AgentMode { get; init; } = "development";

    public string Identity { get; init; } = "unknown";

    public string PipeName { get; init; } = PipeConstants.DefaultPipeName;

    public bool CurrentUserOnly { get; init; }

    public bool DestructiveOperationsEnabled { get; init; }

    public bool RunningAsService { get; init; }

    public string TransportMode { get; init; } = AgentTransportMode.Auto.ToString();

    public string[] EnabledTransports { get; init; } = [];

    public string AuditLogPath { get; init; } = string.Empty;

    public DateTimeOffset StartedAtUtc { get; init; }

    public string[] AllowedCommands { get; init; } = [];
}
