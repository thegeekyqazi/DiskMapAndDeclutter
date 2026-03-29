using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using StorageVisualizer.App.Configuration;
using StorageVisualizer.App.Models;

namespace StorageVisualizer.App.Services;

public sealed class ReviewWorkspaceStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static readonly HashSet<string> AllowedDecisions = new(StringComparer.OrdinalIgnoreCase)
    {
        "none",
        "keep",
        "later",
        "archive",
        "delete"
    };

    private readonly ReviewWorkspaceOptions _options;
    private readonly string _dataDirectory;

    public ReviewWorkspaceStore(IOptions<ReviewWorkspaceOptions> options, IWebHostEnvironment environment)
    {
        _options = options.Value;
        _dataDirectory = Path.GetFullPath(Path.Combine(environment.ContentRootPath, _options.DataDirectory));
        Directory.CreateDirectory(_dataDirectory);
    }

    public async Task<ReviewState> LoadAsync(string rootPath, CancellationToken cancellationToken)
    {
        var normalizedRoot = NormalizeRootPath(rootPath);
        var path = GetStoragePath(normalizedRoot);
        if (!File.Exists(path))
        {
            return new ReviewState
            {
                RootPath = normalizedRoot
            };
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        var state = JsonSerializer.Deserialize<ReviewState>(json, JsonOptions);
        if (state is null)
        {
            return new ReviewState
            {
                RootPath = normalizedRoot
            };
        }

        return new ReviewState
        {
            RootPath = normalizedRoot,
            SavedAtUtc = state.SavedAtUtc,
            Entries = state.Entries
        };
    }

    public async Task<ReviewState> SaveAsync(SaveReviewStateRequest request, CancellationToken cancellationToken)
    {
        var normalizedRoot = NormalizeRootPath(request.RootPath);
        var entries = SanitizeEntries(normalizedRoot, request.Entries);

        var state = new ReviewState
        {
            RootPath = normalizedRoot,
            SavedAtUtc = DateTimeOffset.UtcNow,
            Entries = entries
        };

        var path = GetStoragePath(normalizedRoot);
        var json = JsonSerializer.Serialize(state, JsonOptions);
        await File.WriteAllTextAsync(path, json, cancellationToken);
        return state;
    }

    private IReadOnlyList<ReviewEntry> SanitizeEntries(string rootPath, IReadOnlyList<ReviewEntry> entries)
    {
        if (entries.Count > _options.MaxEntriesPerRoot)
        {
            throw new InvalidOperationException(
                $"Review state exceeds the limit of {_options.MaxEntriesPerRoot} entries for one scan root.");
        }

        var deduped = new Dictionary<string, ReviewEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Path))
            {
                continue;
            }

            var normalizedPath = Path.GetFullPath(entry.Path.Trim());
            if (!IsWithinPath(normalizedPath, rootPath))
            {
                throw new InvalidOperationException(
                    $"Review entry '{normalizedPath}' is outside the current scan root.");
            }

            var decision = NormalizeDecision(entry.Decision);
            var note = (entry.Note ?? string.Empty).Trim();
            if (note.Length > _options.MaxNoteLength)
            {
                note = note[.._options.MaxNoteLength];
            }

            if (decision == "none" &&
                string.IsNullOrWhiteSpace(note) &&
                !entry.IsReviewed &&
                !entry.IsHidden &&
                !entry.IsFavorite)
            {
                deduped.Remove(normalizedPath);
                continue;
            }

            deduped[normalizedPath] = new ReviewEntry
            {
                Path = normalizedPath,
                Decision = decision,
                IsReviewed = entry.IsReviewed,
                IsHidden = entry.IsHidden,
                IsFavorite = entry.IsFavorite,
                Note = note,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            };
        }

        return deduped.Values
            .OrderBy(value => value.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private string GetStoragePath(string normalizedRoot)
    {
        using var sha = SHA256.Create();
        var hash = Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(normalizedRoot)));
        return Path.Combine(_dataDirectory, $"{hash}.json");
    }

    private static string NormalizeRootPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("Root path is required.");
        }

        return Path.GetFullPath(path.Trim());
    }

    private static bool IsWithinPath(string candidatePath, string rootPath)
    {
        var normalizedCandidate = Path.TrimEndingDirectorySeparator(candidatePath);
        var normalizedRoot = Path.TrimEndingDirectorySeparator(rootPath);

        return normalizedCandidate.Equals(normalizedRoot, StringComparison.OrdinalIgnoreCase) ||
               normalizedCandidate.StartsWith(
                   normalizedRoot + Path.DirectorySeparatorChar,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeDecision(string? value)
    {
        var candidate = string.IsNullOrWhiteSpace(value) ? "none" : value.Trim();
        return AllowedDecisions.Contains(candidate) ? candidate.ToLowerInvariant() : "none";
    }
}
