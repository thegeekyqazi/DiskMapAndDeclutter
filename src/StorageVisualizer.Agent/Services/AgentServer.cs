using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Runtime.Versioning;
using StorageVisualizer.Protocol;

namespace StorageVisualizer.Agent.Services;

public sealed class AgentServer
{
    private static readonly UTF8Encoding Utf8NoBom = new(false);
    private readonly AgentOptions _options;
    private readonly AgentRequestProcessor _processor;
    private readonly IAuditLogger _auditLogger;

    public AgentServer(
        AgentOptions options,
        AgentRequestProcessor processor,
        IAuditLogger auditLogger)
    {
        _options = options;
        _processor = processor;
        _auditLogger = auditLogger;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var transportMode = ParseTransportMode(_options.TransportMode);
        var serverTasks = new List<Task>();

        if (transportMode is AgentTransportMode.Auto or AgentTransportMode.PipeOnly)
        {
            serverTasks.Add(RunPipeServerAsync(cancellationToken));
        }

        if (transportMode is AgentTransportMode.Auto or AgentTransportMode.LoopbackOnly)
        {
            serverTasks.Add(RunLoopbackServerAsync(cancellationToken));
        }

        if (serverTasks.Count == 0)
        {
            throw new InvalidOperationException("No agent transports are enabled.");
        }

        await Task.WhenAll(serverTasks);
    }

    private async Task RunPipeServerAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await using var pipe = OperatingSystem.IsWindows()
                ? CreatePipeServer()
                : throw new PlatformNotSupportedException("Named pipes are only configured for Windows in this agent.");

            try
            {
                await pipe.WaitForConnectionAsync(cancellationToken);
                await HandleConnectionAsync(pipe, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                await _auditLogger.WriteAsync(
                    new AuditEntry
                    {
                        RequestId = string.Empty,
                        Command = "pipe-server-error",
                        ClientName = "agent",
                        CallerIdentity = "unknown",
                        Transport = AgentTransportKind.NamedPipe.ToString(),
                        Outcome = "error",
                        Detail = ex.Message
                    },
                    cancellationToken);
            }
        }
    }

    private async Task RunLoopbackServerAsync(CancellationToken cancellationToken)
    {
        using var listener = new TcpListener(IPAddress.Loopback, _options.LoopbackPort);
        listener.Start();
        using var _ = cancellationToken.Register(listener.Stop);

        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient? client = null;
            try
            {
                client = await listener.AcceptTcpClientAsync(cancellationToken);
                await HandleLoopbackConnectionAsync(client, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (SocketException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                await _auditLogger.WriteAsync(
                    new AuditEntry
                    {
                        RequestId = string.Empty,
                        Command = "loopback-server-error",
                        ClientName = "agent",
                        CallerIdentity = "127.0.0.1",
                        Transport = AgentTransportKind.Loopback.ToString(),
                        Outcome = "error",
                        Detail = ex.Message
                    },
                    cancellationToken);
            }
            finally
            {
                client?.Dispose();
            }
        }
    }

    private async Task HandleConnectionAsync(NamedPipeServerStream pipe, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(pipe, Utf8NoBom, leaveOpen: true);
        using var writer = new StreamWriter(pipe, Utf8NoBom, leaveOpen: true) { AutoFlush = true };
        await ProcessRequestAsync(reader, writer, TryGetCallerIdentity(pipe), "named-pipe", cancellationToken);
    }

    private async Task HandleLoopbackConnectionAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Utf8NoBom, leaveOpen: true);
        using var writer = new StreamWriter(stream, Utf8NoBom, leaveOpen: true) { AutoFlush = true };
        await ProcessRequestAsync(
            reader,
            writer,
            client.Client.RemoteEndPoint?.ToString() ?? "127.0.0.1",
            "loopback",
            cancellationToken);
    }

    private async Task ProcessRequestAsync(
        StreamReader reader,
        StreamWriter writer,
        string callerIdentity,
        string transport,
        CancellationToken cancellationToken)
    {
        var line = await reader.ReadLineAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(line))
        {
            await _auditLogger.WriteAsync(
                    new AuditEntry
                    {
                        RequestId = string.Empty,
                        Command = $"{transport}-empty-request",
                        ClientName = "unknown",
                        CallerIdentity = callerIdentity,
                        Transport = transport,
                        Outcome = "denied",
                        Detail = "The transport connected but no request payload was received."
                    },
                cancellationToken);
            return;
        }

        AgentRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<AgentRequest>(line);
        }
        catch (JsonException ex)
        {
            await _auditLogger.WriteAsync(
                    new AuditEntry
                    {
                        RequestId = string.Empty,
                        Command = $"{transport}-invalid-json",
                        ClientName = "unknown",
                        CallerIdentity = callerIdentity,
                        Transport = transport,
                        Outcome = "denied",
                        Detail = ex.Message
                    },
                cancellationToken);
            return;
        }

        if (request is null)
        {
            await _auditLogger.WriteAsync(
                    new AuditEntry
                    {
                        RequestId = string.Empty,
                        Command = $"{transport}-null-request",
                        ClientName = "unknown",
                        CallerIdentity = callerIdentity,
                        Transport = transport,
                        Outcome = "denied",
                        Detail = "The request payload could not be materialized."
                },
                cancellationToken);
            return;
        }

        var response = await _processor.ProcessAsync(request, callerIdentity, cancellationToken);
        var payload = JsonSerializer.Serialize(response);
        await writer.WriteLineAsync(payload);
    }

    [SupportedOSPlatform("windows")]
    private NamedPipeServerStream CreatePipeServer()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Named pipes are only configured for Windows in this agent.");
        }

        return NamedPipeServerStreamAcl.Create(
            _options.PipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            0,
            0,
            BuildPipeSecurity(),
            HandleInheritability.None);
    }

    [SupportedOSPlatform("windows")]
    private PipeSecurity BuildPipeSecurity()
    {
        var security = new PipeSecurity();
        var currentUser = WindowsIdentity.GetCurrent().User;
        if (currentUser is not null)
        {
            security.AddAccessRule(
                new PipeAccessRule(
                    currentUser,
                    PipeAccessRights.FullControl,
                    AccessControlType.Allow));
        }

        security.AddAccessRule(
            new PipeAccessRule(
                new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                PipeAccessRights.FullControl,
                AccessControlType.Allow));

        security.AddAccessRule(
            new PipeAccessRule(
                new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
                PipeAccessRights.FullControl,
                AccessControlType.Allow));

        return security;
    }

    private static AgentTransportMode ParseTransportMode(string? value)
    {
        return Enum.TryParse<AgentTransportMode>(value, ignoreCase: true, out var parsed)
            ? parsed
            : AgentTransportMode.Auto;
    }

    private static string TryGetCallerIdentity(NamedPipeServerStream pipe)
    {
        try
        {
            return pipe.GetImpersonationUserName();
        }
        catch
        {
            return "unavailable";
        }
    }
}
