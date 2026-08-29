using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AskARabbiLIB.Models;

namespace AskARabbiLIB.Retrieval;

/// <summary>Creates bounded, parseable Azure search documents from normalized Sefaria Markdown.</summary>
public sealed class AzureOpenAIVectorStoreCorpusFormatter
{
    /// <summary>Gets the maximum UTF-8 size of one provider upload artifact.</summary>
    public const int MaximumUploadBytes = 60_000;

    /// <summary>Gets the maximum exact source characters stored in one searchable record.</summary>
    public const int MaximumRecordCharacters = 1_500;

    /// <summary>Gets the character overlap used when an overlong canonical segment is explicitly excerpted.</summary>
    public const int RecordOverlapCharacters = 300;

    private static readonly JsonSerializerOptions EnvelopeJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly NormalizedMarkdownSegmentParser parser = new();

    /// <summary>Formats one checksum-verified normalized document for managed vector-store ingestion.</summary>
    /// <param name="document">Trusted manifest metadata.</param>
    /// <param name="markdown">Checksum-verified normalized Markdown.</param>
    /// <param name="corpusFingerprint">Fingerprint shared by the entire publication.</param>
    /// <returns>A deterministic UTF-8 upload artifact.</returns>
    public AzureOpenAIVectorStoreCorpusDocument Format(ManifestDocument document, string markdown, string corpusFingerprint)
    {
        var parts = FormatParts(document, markdown, corpusFingerprint);
        if (parts.Count != 1)
        {
            throw new InvalidOperationException($"Document '{document.DocumentId}' requires {parts.Count} upload parts; use FormatParts for managed publication.");
        }
        return parts[0];
    }

    /// <summary>Formats one checksum-verified normalized document into deterministic bounded upload artifacts.</summary>
    /// <param name="document">Trusted manifest metadata.</param>
    /// <param name="markdown">Checksum-verified normalized Markdown.</param>
    /// <param name="corpusFingerprint">Fingerprint shared by the entire publication.</param>
    /// <returns>One or more UTF-8 artifacts whose records are never split across files.</returns>
    public IReadOnlyList<AzureOpenAIVectorStoreCorpusDocument> FormatParts(ManifestDocument document, string markdown, string corpusFingerprint)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(markdown);
        ValidateFingerprint(corpusFingerprint);
        var segments = parser.Parse(document, markdown);
        var attributes = CreateAttributes(document, corpusFingerprint);
        var header = CreateHeader(document);
        var headerByteCount = Encoding.UTF8.GetByteCount(header);
        var parts = new List<PartAccumulator>();
        var current = new PartAccumulator(header, headerByteCount);
        foreach (var segment in segments)
        {
            foreach (var record in CreateSegmentRecords(segment))
            {
                var recordByteCount = Encoding.UTF8.GetByteCount(record.Content);
                if (headerByteCount + recordByteCount > MaximumUploadBytes)
                {
                    throw new InvalidDataException($"Segment record '{record.SegmentId}' exceeds the managed upload limit.");
                }
                if (current.SearchRecordCount > 0 && current.ContentByteCount + recordByteCount > MaximumUploadBytes)
                {
                    parts.Add(current);
                    current = new PartAccumulator(header, headerByteCount);
                }

                current.Builder.Append(record.Content);
                current.ContentByteCount += recordByteCount;
                current.SearchRecordCount++;
                if (record.IsFirstSourceRecord)
                {
                    current.SourceSegmentCount++;
                }
            }
        }
        if (current.SearchRecordCount > 0)
        {
            parts.Add(current);
        }
        if (parts.Count == 0)
        {
            throw new InvalidDataException($"Document '{document.DocumentId}' produced no searchable records.");
        }

        return parts.Select((part, index) => new AzureOpenAIVectorStoreCorpusDocument(
            CreateFileName(document, index + 1, parts.Count),
            Encoding.UTF8.GetBytes(part.Builder.ToString()),
            attributes,
            part.SourceSegmentCount,
            part.SearchRecordCount)).ToArray();
    }

    internal static string CreateLookupToken(string documentId, int documentOrdinal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);
        if (documentOrdinal < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(documentOrdinal), "Document ordinal cannot be negative.");
        }

        var value = Encoding.UTF8.GetBytes($"{documentId}|{documentOrdinal}");
        return $"AARLOOKUP{Convert.ToHexString(SHA256.HashData(value))[..24]}";
    }

    private static IEnumerable<FormattedRecord> CreateSegmentRecords(SourceSegment segment)
    {
        EnsureMarkerSafe(segment.Text, segment.SegmentId);
        if (segment.Text.Length <= MaximumRecordCharacters)
        {
            yield return new FormattedRecord(segment.SegmentId, CreateRecord(segment, segment.SegmentId, 0, segment.Text, 0, false), true);
            yield break;
        }

        var windowIndex = 0;
        var start = 0;
        while (start < segment.Text.Length)
        {
            var length = Math.Min(MaximumRecordCharacters, segment.Text.Length - start);
            var windowText = segment.Text.Substring(start, length);
            var windowId = $"{segment.SegmentId}:excerpt:{windowIndex + 1:D4}";
            yield return new FormattedRecord(windowId, CreateRecord(segment, windowId, windowIndex, windowText, start, true), windowIndex == 0);
            windowIndex++;
            if (start + length >= segment.Text.Length)
            {
                break;
            }

            start += MaximumRecordCharacters - RecordOverlapCharacters;
        }
    }

    private static string CreateRecord(SourceSegment segment, string recordId, int windowIndex, string text, int excerptStart, bool isExcerpt)
    {
        var envelope = new SegmentEnvelope
        {
            SegmentId = recordId,
            OriginalSegmentId = segment.SegmentId,
            DocumentOrdinal = segment.DocumentOrdinal,
            CanonicalReference = segment.CanonicalReference,
            LookupToken = CreateLookupToken(segment.DocumentId, segment.DocumentOrdinal),
            WindowIndex = windowIndex,
            ExcerptStart = excerptStart,
            OriginalCharacterCount = segment.Text.Length,
            IsExcerpt = isExcerpt,
        };
        var builder = new StringBuilder(text.Length + 512);
        builder.Append(AzureOpenAIVectorStoreCorpusContract.StartMarker).Append('\n')
            .Append(JsonSerializer.Serialize(envelope, EnvelopeJsonOptions)).Append('\n')
            .Append("Source title: ").Append(segment.Title).Append('\n')
            .Append("Canonical reference: ").Append(segment.CanonicalReference).Append('\n')
            .Append("Lookup token: ").Append(envelope.LookupToken).Append('\n')
            .Append(AzureOpenAIVectorStoreCorpusContract.PassageMarker).Append('\n')
            .Append(text).Append('\n')
            .Append(AzureOpenAIVectorStoreCorpusContract.EndMarker).Append("\n\n");
        return builder.ToString();
    }

    private static string CreateHeader(ManifestDocument document)
    {
        var builder = new StringBuilder(512);
        builder.Append("AskARabbi approved Sefaria search document\n")
            .Append("Source title: ").Append(document.FileTitle).Append('\n')
            .Append("Hebrew title: ").Append(document.HebrewTitle).Append('\n')
            .Append("Language: ").Append(document.FileLanguage).Append('\n')
            .Append("Collection: ").Append(document.Collection).Append('\n')
            .Append("Categories: ").Append(string.Join(", ", document.Categories)).Append('\n')
            .Append("Edition: ").Append(document.VersionTitle).Append('\n');
        if (!string.IsNullOrWhiteSpace(document.WorkKey))
        {
            builder.Append("Work key: ").Append(document.WorkKey).Append('\n');
        }
        builder.Append('\n');
        return builder.ToString();
    }

    private static IReadOnlyDictionary<string, string> CreateAttributes(ManifestDocument document, string corpusFingerprint)
    {
        var attributes = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [AzureOpenAIVectorStoreCorpusContract.CorpusFingerprintAttribute] = corpusFingerprint,
            [AzureOpenAIVectorStoreCorpusContract.DocumentIdAttribute] = document.DocumentId,
            [AzureOpenAIVectorStoreCorpusContract.TitleAttribute] = document.FileTitle,
            [AzureOpenAIVectorStoreCorpusContract.HebrewTitleAttribute] = document.HebrewTitle,
            [AzureOpenAIVectorStoreCorpusContract.LanguageAttribute] = document.FileLanguage,
            [AzureOpenAIVectorStoreCorpusContract.LanguageCodeAttribute] = document.FileLanguageCode,
            [AzureOpenAIVectorStoreCorpusContract.CollectionAttribute] = document.Collection,
            [AzureOpenAIVectorStoreCorpusContract.CategoriesAttribute] = JsonSerializer.Serialize(document.Categories),
            [AzureOpenAIVectorStoreCorpusContract.VersionAttribute] = document.VersionTitle,
            [AzureOpenAIVectorStoreCorpusContract.LicenseAttribute] = document.License,
            [AzureOpenAIVectorStoreCorpusContract.LicenseCategoryAttribute] = document.LicenseCategory.ToString(),
            [AzureOpenAIVectorStoreCorpusContract.SourceUrlAttribute] = document.AttributionUrl,
            [AzureOpenAIVectorStoreCorpusContract.FilePathAttribute] = document.FilePath,
            [AzureOpenAIVectorStoreCorpusContract.WorkKeyAttribute] = document.WorkKey ?? string.Empty,
            [AzureOpenAIVectorStoreCorpusContract.UsageNoteAttribute] = document.UsageNote ?? string.Empty,
            [AzureOpenAIVectorStoreCorpusContract.SourceProviderAttribute] = "Sefaria",
        };
        if (attributes.Count != AzureOpenAIVectorStoreCorpusContract.MaximumAttributes || attributes.Any(pair => pair.Key.Length > 64 || pair.Value.Length > 512))
        {
            throw new InvalidDataException($"Vector-store attributes exceed Azure limits for document '{document.DocumentId}'.");
        }

        return attributes;
    }

    private static string CreateFileName(ManifestDocument document, int partNumber, int partCount)
    {
        var suffix = document.RawSha256.Length >= 16 ? document.RawSha256[..16].ToLowerInvariant() : throw new InvalidDataException($"Document '{document.DocumentId}' has an invalid raw checksum.");
        return partCount == 1 ? $"sefaria-{suffix}.md" : $"sefaria-{suffix}-part-{partNumber:D4}.md";
    }

    private static void EnsureMarkerSafe(string text, string segmentId)
    {
        if (text.Contains(AzureOpenAIVectorStoreCorpusContract.StartMarker, StringComparison.Ordinal) || text.Contains(AzureOpenAIVectorStoreCorpusContract.EndMarker, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Segment '{segmentId}' contains a reserved vector-store marker.");
        }
    }

    private static void ValidateFingerprint(string value)
    {
        if (value is not { Length: 64 } || value.Any(character => character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
        {
            throw new ArgumentException("Corpus fingerprint must be a lowercase SHA-256 value.", nameof(value));
        }
    }

    private sealed record SegmentEnvelope
    {
        public required string SegmentId { get; init; }

        public required string OriginalSegmentId { get; init; }

        public required int DocumentOrdinal { get; init; }

        public required string CanonicalReference { get; init; }

        public required string LookupToken { get; init; }

        public required int WindowIndex { get; init; }

        public required int ExcerptStart { get; init; }

        public required int OriginalCharacterCount { get; init; }

        public required bool IsExcerpt { get; init; }
    }

    private sealed record FormattedRecord(string SegmentId, string Content, bool IsFirstSourceRecord);

    private sealed class PartAccumulator
    {
        internal PartAccumulator(string header, int headerByteCount)
        {
            Builder = new StringBuilder(header);
            ContentByteCount = headerByteCount;
        }

        internal StringBuilder Builder { get; }

        internal int ContentByteCount { get; set; }

        internal int SourceSegmentCount { get; set; }

        internal int SearchRecordCount { get; set; }
    }
}

internal static class AzureOpenAIVectorStoreCorpusContract
{
    internal const int MaximumAttributes = 16;
    internal const string StartMarker = "ASKARABBI_SEGMENT_V1_START";
    internal const string EndMarker = "ASKARABBI_SEGMENT_V1_END";
    internal const string PassageMarker = "Passage:";
    internal const string CorpusFingerprintAttribute = "corpusFingerprint";
    internal const string DocumentIdAttribute = "documentId";
    internal const string TitleAttribute = "title";
    internal const string HebrewTitleAttribute = "hebrewTitle";
    internal const string LanguageAttribute = "language";
    internal const string LanguageCodeAttribute = "languageCode";
    internal const string CollectionAttribute = "collection";
    internal const string CategoriesAttribute = "categories";
    internal const string VersionAttribute = "version";
    internal const string LicenseAttribute = "license";
    internal const string LicenseCategoryAttribute = "licenseCategory";
    internal const string SourceUrlAttribute = "sourceUrl";
    internal const string FilePathAttribute = "filePath";
    internal const string WorkKeyAttribute = "workKey";
    internal const string UsageNoteAttribute = "usageNote";
    internal const string SourceProviderAttribute = "sourceProvider";
    internal const string StoreSchemaMetadata = "schemaVersion";
    internal const string StoreFingerprintMetadata = "corpusFingerprint";
    internal const string StoreManifestSchemaMetadata = "manifestSchemaVersion";
    internal const string StoreDocumentCountMetadata = "documentCount";
    internal const string StoreFileCountMetadata = "fileCount";
    internal const string StoreSegmentCountMetadata = "segmentCount";
    internal const string StoreSourceProviderMetadata = "sourceProvider";
    internal const string StoreSchemaVersion = "2";
}
