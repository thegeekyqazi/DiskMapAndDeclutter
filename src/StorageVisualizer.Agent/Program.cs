using StorageVisualizer.Agent.Services;
using StorageVisualizer.Protocol;

var options = AgentOptions.LoadFromCurrentDirectory();
var auditLogger = new FileAuditLogger(options.AuditLogPath);
var processor = new AgentRequestProcessor(options, auditLogger);
var server = new AgentServer(options, processor, auditLogger);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cts.Cancel();
};

Console.WriteLine($"StorageVisualizer.Agent listening on pipe '{options.PipeName}'.");
Console.WriteLine("Dev-phase shared-token auth is enabled. Destructive commands remain disabled.");
Console.WriteLine($"Transport mode: {options.TransportMode}.");
if (!string.Equals(options.TransportMode, AgentTransportMode.PipeOnly.ToString(), StringComparison.OrdinalIgnoreCase))
{
    Console.WriteLine($"Loopback fallback is enabled on 127.0.0.1:{options.LoopbackPort}.");
}

await server.RunAsync(cts.Token);
