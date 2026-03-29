using System.Text.Json;

namespace StorageVisualizer.Agent.Services;

public sealed class FileAuditLogger : IAuditLogger
{
    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public FileAuditLogger(string path)
    {
        _path = path;
    }

    public async Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var line = JsonSerializer.Serialize(entry) + Environment.NewLine;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            await File.AppendAllTextAsync(_path, line, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }
}
