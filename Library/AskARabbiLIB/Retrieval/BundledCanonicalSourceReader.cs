using System.Collections.Concurrent;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using AskARabbiLIB.Models;

namespace AskARabbiLIB.Retrieval;

/// <summary>Reads a bounded cache of complete approved editions from the deployment's immutable source archive.</summary>
public sealed class BundledCanonicalSourceReader : ICanonicalSourceReader
{
    private readonly Func<Stream> openArchive;
    private readonly IReadOnlyList<ManifestDocument> documents;
    private readonly ConcurrentDictionary<string, IReadOnlyList<SourceSegment>> cache = new(StringComparer.Ordinal);

    /// <summary>Opens an archive whose entries are addressed by hashes in the approved manifest.</summary>
    /// <param name="manifest">Validated approved-corpus manifest.</param>
    /// <param name="archivePath">Deployment-local archive, never a user-provided path.</param>
    public BundledCanonicalSourceReader(DocumentManifest manifest, string archivePath) : this(manifest, CreateArchiveFactory(archivePath))
    {
    }

    /// <summary>Opens approved editions through an injected stream factory, including in-memory verification fixtures.</summary>
    /// <param name="manifest">Validated approved-corpus manifest.</param>
    /// <param name="openArchive">Creates a fresh readable archive stream; the reader disposes it.</param>
    public BundledCanonicalSourceReader(DocumentManifest manifest, Func<Stream> openArchive)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        this.openArchive = openArchive ?? throw new ArgumentNullException(nameof(openArchive));
        using var archive = new ZipArchive(openArchive(), ZipArchiveMode.Read);
        var entries = archive.Entries.Select(entry => entry.FullName).ToHashSet(StringComparer.Ordinal);
        documents = manifest.Documents.Where(document => entries.Contains(document.Sha256 + ".md")).ToArray();
        if (documents.Count == 0)
        {
            throw new InvalidDataException("The canonical source archive does not match the approved manifest.");
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SourceSegment>> ReadAsync(string reference, SourceRetrievalQuery filters, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filters);
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        if (!CanonicalReferenceRange.TryParse(reference, out var range) || range is null)
        {
            return [];
        }
        var candidates = documents.Where(document => Matches(document, range.Book, filters))
            .OrderBy(document => LanguageRank(document, filters.Languages))
            .ThenByDescending(document => document.SegmentCount);
        foreach (var document in candidates)
        {
            var segments = await LoadAsync(document, cancellationToken).ConfigureAwait(false);
            var matches = segments.Where(segment => range.Contains(segment.CanonicalReference)).Take(2_500).ToArray();
            if (matches.Length > 0)
            {
                return matches;
            }
        }
        return [];
    }

    private async Task<IReadOnlyList<SourceSegment>> LoadAsync(ManifestDocument document, CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(document.DocumentId, out var cached))
        {
            return cached;
        }
        using var archive = new ZipArchive(openArchive(), ZipArchiveMode.Read);
        var entry = archive.GetEntry(document.Sha256 + ".md") ?? throw new InvalidDataException("A canonical document is missing from the archive.");
        if (entry.Length != document.FileSizeBytes || entry.Length > 20_000_000)
        {
            throw new InvalidDataException("A canonical document has invalid size metadata.");
        }
        await using var stream = entry.Open();
        using var buffer = new MemoryStream((int)entry.Length);
        await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        var bytes = buffer.ToArray();
        if (!string.Equals(Convert.ToHexStringLower(SHA256.HashData(bytes)), document.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("A canonical source document failed checksum verification.");
        }
        var parsed = new NormalizedMarkdownSegmentParser().Parse(document, Encoding.UTF8.GetString(bytes));
        var segments = document.WorkKey == "shulchan_arukh_with_rema"
            ? parsed.Select(segment => segment with { UsageNote = segment.UsageNote + " Attribution boundary: this edition interleaves Rabbi Yosef Karo's base text and Rema's glosses. Parenthesized Rema remarks may end before the base sentence resumes. Do not attribute the whole segment to Rema merely because it contains a Rema marker; when the speaker is ambiguous, cite Shulchan Arukh without inventing an individual attribution." }).ToArray()
            : parsed;
        // Bound memory on the small API container; cold books remain readable without caching.
        if (cache.Count < 16 && bytes.Length < 2_000_000)
        {
            cache.TryAdd(document.DocumentId, segments);
        }
        return segments;
    }

    private static bool Matches(ManifestDocument document, string book, SourceRetrievalQuery filters)
    {
        return CanonicalReferenceRange.TryParse(document.FirstReference ?? string.Empty, out var first) && first is not null
            && string.Equals(first.Book, book, StringComparison.OrdinalIgnoreCase)
            && (filters.Languages.Count == 0 || filters.Languages.Any(language => string.Equals(language, document.FileLanguage, StringComparison.OrdinalIgnoreCase) || string.Equals(language, document.FileLanguageCode, StringComparison.OrdinalIgnoreCase)))
            && (filters.Collections.Count == 0 || filters.Collections.Contains(document.Collection, StringComparer.OrdinalIgnoreCase))
            && (filters.Categories.Count == 0 || document.Categories.Any(category => filters.Categories.Contains(category, StringComparer.OrdinalIgnoreCase)))
            && (filters.WorkKeys.Count == 0 || document.WorkKey is not null && filters.WorkKeys.Contains(document.WorkKey, StringComparer.Ordinal))
            && (filters.SourceKeys.Count == 0 || filters.SourceKeys.Contains(document.WorkKey is null ? $"collection:{document.Collection}" : $"work:{document.WorkKey}", StringComparer.Ordinal));
    }

    private static Func<Stream> CreateArchiveFactory(string archivePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);
        var path = Path.GetFullPath(archivePath);
        return () => File.OpenRead(path);
    }

    private static int LanguageRank(ManifestDocument document, IReadOnlyCollection<string> languages)
    {
        var index = 0;
        foreach (var language in languages)
        {
            if (string.Equals(document.FileLanguage, language, StringComparison.OrdinalIgnoreCase) || string.Equals(document.FileLanguageCode, language, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
            index++;
        }
        return document.FileLanguageCode == "en" ? 0 : 1;
    }
}
