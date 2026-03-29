namespace StorageVisualizer.Agent.Services;

public interface IAuditLogger
{
    Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken);
}

public sealed class AuditEntry
{
    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;

    public string RequestId { get; init; } = string.Empty;

    public string Command { get; init; } = string.Empty;

    public string ClientName { get; init; } = string.Empty;

    public string CallerIdentity { get; init; } = string.Empty;

    public string Transport { get; init; } = string.Empty;

    public string Outcome { get; init; } = string.Empty;

    public string Detail { get; init; } = string.Empty;
}
