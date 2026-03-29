using System.IO.Compression;
using System.Text;
using Microsoft.Extensions.Options;
using StorageVisualizer.App.Configuration;
using StorageVisualizer.App.Models;

namespace StorageVisualizer.App.Services;

public sealed class FileInspectionService
{
    private readonly StorageScanOptions _options;
    private readonly SafeFileEnumerator _fileEnumerator;
    private readonly InstalledProgramCatalog _installedProgramCatalog;
    private readonly FileLockInspector _fileLockInspector;
    private readonly HashSet<string> _textExtensions;

    public FileInspectionService(
        IOptions<StorageScanOptions> options,
        SafeFileEnumerator fileEnumerator,
        InstalledProgramCatalog installedProgramCatalog,
        FileLockInspector fileLockInspector)
    {
        _options = options.Value;
        _fileEnumerator = fileEnumerator;
        _installedProgramCatalog = installedProgramCatalog;
        _fileLockInspector = fileLockInspector;
        _textExtensions = new HashSet<string>(_options.TextPreviewExtensions, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<FileInspectionResponse> InspectAsync(
        string rootPath,
        string targetPath,
        CancellationToken cancellationToken)
    {
        var rootDirectory = _fileEnumerator.GetValidatedRootDirectory(rootPath);
        var normalizedTarget = _fileEnumerator.NormalizeAndValidateChildPath(rootDirectory.FullName, targetPath);
        var file = new FileInfo(normalizedTarget);
        if (!file.Exists)
        {
            throw new FileNotFoundException("The selected file does not exist.", normalizedTarget);
        }

        if (file.Attributes.HasFlag(FileAttributes.Directory))
        {
            throw new InvalidOperationException("File inspection only supports files right now.");
        }

        var isProtected = _installedProgramCatalog.TryMatch(file.FullName, out var program);
        var preview = await BuildPreviewAsync(file, cancellationToken);

        return new FileInspectionResponse
        {
            RootPath = rootDirectory.FullName,
            TargetPath = file.FullName,
            Name = file.Name,
            Extension = file.Extension,
            SizeBytes = file.Length,
            CreatedUtc = ToUtcOffset(file.CreationTimeUtc),
            LastWriteUtc = ToUtcOffset(file.LastWriteTimeUtc),
            LastAccessUtc = ToUtcOffset(file.LastAccessTimeUtc),
            Attributes = file.Attributes.ToString(),
            IsProtected = isProtected,
            ProtectionReason = isProtected ? $"Matches installed application path: {program.DisplayName}" : string.Empty,
            IsProbablyLocked = _fileLockInspector.IsProbablyLocked(file.FullName),
            PreviewKind = preview.Kind,
            PreviewTitle = preview.Title,
            PreviewLines = preview.Lines,
            Summary = preview.Summary
        };
    }

    private async Task<(string Kind, string Title, string Summary, IReadOnlyList<string> Lines)> BuildPreviewAsync(
        FileInfo file,
        CancellationToken cancellationToken)
    {
        if (string.Equals(file.Extension, ".zip", StringComparison.OrdinalIgnoreCase))
        {
            return await BuildZipPreviewAsync(file, cancellationToken);
        }

        if (_textExtensions.Contains(file.Extension))
        {
            return await BuildTextPreviewAsync(file, cancellationToken);
        }

        return await BuildBinaryPreviewAsync(file, cancellationToken);
    }

    private async Task<(string Kind, string Title, string Summary, IReadOnlyList<string> Lines)> BuildTextPreviewAsync(
        FileInfo file,
        CancellationToken cancellationToken)
    {
        var lines = new List<string>();
        using var stream = file.OpenRead();
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        while (!reader.EndOfStream && lines.Count < _options.InspectionPreviewLineLimit)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(cancellationToken) ?? string.Empty;
            lines.Add(line.Length > 240 ? $"{line[..240]}..." : line);
        }

        return (
            "text",
            "Text preview",
            "Showing the first readable lines from this file. Nothing is being modified.",
            lines);
    }

    private Task<(string Kind, string Title, string Summary, IReadOnlyList<string> Lines)> BuildZipPreviewAsync(
        FileInfo file,
        CancellationToken cancellationToken)
    {
        var lines = new List<string>();
        using var archive = ZipFile.OpenRead(file.FullName);
        foreach (var entry in archive.Entries.Take(_options.InspectionZipEntryLimit))
        {
            cancellationToken.ThrowIfCancellationRequested();
            lines.Add($"{entry.FullName} ({entry.Length} bytes)");
        }

        if (archive.Entries.Count > _options.InspectionZipEntryLimit)
        {
            lines.Add($"...and {archive.Entries.Count - _options.InspectionZipEntryLimit} more entries");
        }

        return Task.FromResult((
            "zip",
            "Archive contents",
            "Showing entry names inside this archive. Nothing is extracted or modified.",
            (IReadOnlyList<string>)lines));
    }

    private async Task<(string Kind, string Title, string Summary, IReadOnlyList<string> Lines)> BuildBinaryPreviewAsync(
        FileInfo file,
        CancellationToken cancellationToken)
    {
        var byteCount = Math.Min(_options.InspectionHexByteCount, (int)Math.Min(file.Length, int.MaxValue));
        var buffer = new byte[byteCount];
        await using var stream = file.OpenRead();
        var read = await stream.ReadAsync(buffer.AsMemory(0, byteCount), cancellationToken);
        var hex = Convert.ToHexString(buffer.AsSpan(0, read));

        var formatted = new List<string>();
        for (var index = 0; index < hex.Length; index += 32)
        {
            formatted.Add(hex.Substring(index, Math.Min(32, hex.Length - index)));
        }

        return (
            "hex",
            "Binary preview",
            "This file does not look like plain text, so the preview shows the first bytes in hex.",
            formatted);
    }

    private static DateTimeOffset ToUtcOffset(DateTime value)
    {
        return new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    }
}
