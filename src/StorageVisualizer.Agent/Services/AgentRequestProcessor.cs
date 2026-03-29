using StorageVisualizer.Protocol;

namespace StorageVisualizer.Agent.Services;

public sealed class AgentRequestProcessor
{
    private static readonly AgentCommandType[] AllowedCommands =
    [
        AgentCommandType.Ping,
        AgentCommandType.GetStatus
    ];

    private readonly AgentOptions _options;
    private readonly IAuditLogger _auditLogger;

    public AgentRequestProcessor(AgentOptions options, IAuditLogger auditLogger)
    {
        _options = options;
        _auditLogger = auditLogger;
    }

    public async Task<AgentResponse> ProcessAsync(
        AgentRequest request,
        string callerIdentity,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateRequest(request);
        if (validationError is not null)
        {
            var invalid = new AgentResponse
            {
                RequestId = request.RequestId,
                Success = false,
                Allowed = false,
                Message = validationError
            };

            await WriteAuditAsync(request, callerIdentity, "denied", validationError, cancellationToken);
            return invalid;
        }

        if (!string.Equals(request.AuthToken, _options.ClientToken, StringComparison.Ordinal))
        {
            var denied = new AgentResponse
            {
                RequestId = request.RequestId,
                Success = false,
                Allowed = false,
                Message = "Authentication failed."
            };

            await WriteAuditAsync(request, callerIdentity, "denied", denied.Message, cancellationToken);
            return denied;
        }

        AgentResponse response;

        switch (request.Command)
        {
            case AgentCommandType.Ping:
                response = new AgentResponse
                {
                    RequestId = request.RequestId,
                    Success = true,
                    Allowed = true,
                    Message = "Agent reachable."
                };
                break;

            case AgentCommandType.GetStatus:
                response = new AgentResponse
                {
                    RequestId = request.RequestId,
                    Success = true,
                    Allowed = true,
                    Message = "Agent status returned.",
                    Status = new AgentStatus
                    {
                        AgentMode = _options.AgentMode,
                        Identity = Environment.UserName,
                        PipeName = _options.PipeName,
                        CurrentUserOnly = _options.CurrentUserOnly,
                        DestructiveOperationsEnabled = _options.DestructiveOperationsEnabled,
                        RunningAsService = _options.RunningAsService,
                        TransportMode = _options.TransportMode,
                        EnabledTransports = GetEnabledTransports(_options.TransportMode),
                        AuditLogPath = _options.AuditLogPath,
                        StartedAtUtc = _options.StartedAtUtc,
                        AllowedCommands = AllowedCommands.Select(value => value.ToString()).ToArray()
                    }
                };
                break;

            default:
                response = new AgentResponse
                {
                    RequestId = request.RequestId,
                    Success = false,
                    Allowed = false,
                    Message = "Command denied. Destructive operations are not enabled in this phase."
                };
                break;
        }

        await WriteAuditAsync(
            request,
            callerIdentity,
            response.Allowed ? "allowed" : "denied",
            response.Message,
            cancellationToken);

        return response;
    }

    private static string? ValidateRequest(AgentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RequestId))
        {
            return "Request ID is required.";
        }

        if (string.IsNullOrWhiteSpace(request.ClientName))
        {
            return "Client name is required.";
        }

        if (request.Command is not AgentCommandType.Ping and not AgentCommandType.GetStatus and not AgentCommandType.RequestDeletePath)
        {
            return "Unknown command.";
        }

        if (request.Command == AgentCommandType.RequestDeletePath && string.IsNullOrWhiteSpace(request.TargetPath))
        {
            return "Target path is required for delete requests.";
        }

        return null;
    }

    private async Task WriteAuditAsync(
        AgentRequest request,
        string callerIdentity,
        string outcome,
        string detail,
        CancellationToken cancellationToken)
    {
        await _auditLogger.WriteAsync(
            new AuditEntry
            {
                RequestId = request.RequestId,
                Command = request.Command.ToString(),
                ClientName = request.ClientName,
                CallerIdentity = callerIdentity,
                Transport = request.Transport.ToString(),
                Outcome = outcome,
                Detail = detail
            },
            cancellationToken);
    }

    private static string[] GetEnabledTransports(string? transportMode)
    {
        var mode = Enum.TryParse<AgentTransportMode>(transportMode, ignoreCase: true, out var parsed)
            ? parsed
            : AgentTransportMode.Auto;

        return mode switch
        {
            AgentTransportMode.PipeOnly => [AgentTransportKind.NamedPipe.ToString()],
            AgentTransportMode.LoopbackOnly => [AgentTransportKind.Loopback.ToString()],
            _ => [AgentTransportKind.NamedPipe.ToString(), AgentTransportKind.Loopback.ToString()]
        };
    }
}
