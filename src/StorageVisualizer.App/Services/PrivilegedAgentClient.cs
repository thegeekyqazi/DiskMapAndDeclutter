using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using StorageVisualizer.App.Configuration;
using StorageVisualizer.App.Models;
using StorageVisualizer.Protocol;

namespace StorageVisualizer.App.Services;

public sealed class PrivilegedAgentClient
{
    private static readonly UTF8Encoding Utf8NoBom = new(false);
    private readonly PrivilegedAgentOptions _options;

    public PrivilegedAgentClient(IOptions<PrivilegedAgentOptions> options)
    {
        _options = options.Value;
    }

    public async Task<PrivilegedAgentStatusResponse> GetStatusAsync(CancellationToken cancellationToken)
    {
        var transportMode = ParseTransportMode(_options.TransportMode);

        if (transportMode is AgentTransportMode.Auto or AgentTransportMode.PipeOnly)
        {
            var pipeAttempt = await TryGetStatusOverNamedPipeAsync(cancellationToken);
            if (pipeAttempt.IsReachable || transportMode == AgentTransportMode.PipeOnly)
            {
                return pipeAttempt;
            }

            if (transportMode == AgentTransportMode.Auto)
            {
                var loopbackAttempt = await TryGetStatusOverLoopbackAsync(cancellationToken);
                return loopbackAttempt.IsReachable ? loopbackAttempt : pipeAttempt;
            }
        }

        return await TryGetStatusOverLoopbackAsync(cancellationToken);
    }

    private async Task<PrivilegedAgentStatusResponse> TryGetStatusOverNamedPipeAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var client = new NamedPipeClientStream(
                ".",
                _options.PipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_options.TimeoutMilliseconds);

            await client.ConnectAsync(timeoutCts.Token);

            using var writer = new StreamWriter(client, Utf8NoBom, leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(client, Utf8NoBom, leaveOpen: true);

            var request = new AgentRequest
            {
                ClientName = "StorageVisualizer.App",
                AuthToken = _options.ClientToken,
                Command = AgentCommandType.GetStatus,
                Transport = AgentTransportKind.NamedPipe
            };

            await writer.WriteLineAsync(JsonSerializer.Serialize(request));
            var payload = await reader.ReadLineAsync(timeoutCts.Token);
            if (string.IsNullOrWhiteSpace(payload))
            {
                return BuildUnreachable("Agent returned an empty response.", "named-pipe");
            }

            var response = JsonSerializer.Deserialize<AgentResponse>(payload);
            if (response is null)
            {
                return BuildUnreachable("Agent response could not be parsed.", "named-pipe");
            }

            return new PrivilegedAgentStatusResponse
            {
                IsReachable = response.Success && response.Allowed,
                Message = response.Message,
                Transport = "named-pipe",
                ConfiguredTransportMode = _options.TransportMode,
                Status = response.Status
            };
        }
        catch (OperationCanceledException)
        {
            return BuildUnreachable("Agent connection timed out.");
        }
        catch (IOException)
        {
            return BuildUnreachable("Agent is offline.");
        }
        catch (UnauthorizedAccessException)
        {
            return BuildUnreachable("Agent access was denied.");
        }
    }

    private async Task<PrivilegedAgentStatusResponse> TryGetStatusOverLoopbackAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var client = new TcpClient();
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_options.TimeoutMilliseconds);

            await client.ConnectAsync(IPAddress.Loopback, _options.LoopbackPort, timeoutCts.Token);

            using var stream = client.GetStream();
            using var writer = new StreamWriter(stream, Utf8NoBom, leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(stream, Utf8NoBom, leaveOpen: true);

            var request = new AgentRequest
            {
                ClientName = "StorageVisualizer.App",
                AuthToken = _options.ClientToken,
                Command = AgentCommandType.GetStatus,
                Transport = AgentTransportKind.Loopback
            };

            await writer.WriteLineAsync(JsonSerializer.Serialize(request));
            var payload = await reader.ReadLineAsync(timeoutCts.Token);
            if (string.IsNullOrWhiteSpace(payload))
            {
                return BuildUnreachable("Agent returned an empty loopback response.");
            }

            var response = JsonSerializer.Deserialize<AgentResponse>(payload);
            if (response is null)
            {
                return BuildUnreachable("Agent loopback response could not be parsed.");
            }

            return new PrivilegedAgentStatusResponse
            {
                IsReachable = response.Success && response.Allowed,
                Message = response.Message,
                Transport = "loopback",
                ConfiguredTransportMode = _options.TransportMode,
                Status = response.Status
            };
        }
        catch (OperationCanceledException)
        {
            return BuildUnreachable("Agent loopback connection timed out.");
        }
        catch (SocketException)
        {
            return BuildUnreachable("Agent loopback fallback is offline.");
        }
        catch (IOException)
        {
            return BuildUnreachable("Agent loopback fallback is offline.");
        }
        catch (UnauthorizedAccessException)
        {
            return BuildUnreachable("Agent loopback access was denied.");
        }
    }

    private PrivilegedAgentStatusResponse BuildUnreachable(string message, string? transport = null)
    {
        return new PrivilegedAgentStatusResponse
        {
            IsReachable = false,
            Message = message,
            Transport = transport ?? "unavailable",
            ConfiguredTransportMode = _options.TransportMode
        };
    }

    private static AgentTransportMode ParseTransportMode(string? value)
    {
        return Enum.TryParse<AgentTransportMode>(value, ignoreCase: true, out var parsed)
            ? parsed
            : AgentTransportMode.Auto;
    }
}
