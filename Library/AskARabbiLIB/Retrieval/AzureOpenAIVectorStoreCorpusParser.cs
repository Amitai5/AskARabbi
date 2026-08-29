using System.Text.Json;
using System.Text.Json.Serialization;
using AskARabbiLIB.Models;

namespace AskARabbiLIB.Retrieval;

/// <summary>Reconstructs trusted source segments from complete managed-search records.</summary>
public sealed class AzureOpenAIVectorStoreCorpusParser
{
    private static readonly JsonSerializerOptions EnvelopeJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };
    private readonly IReadOnlyDictionary<string, ManifestDocument>? documents;

    /// <summary>Creates a parser that requires Azure to return complete trusted file attributes.</summary>
    public AzureOpenAIVectorStoreCorpusParser()
    {
    }

    /// <summary>Creates a parser that can resolve trusted provenance from the bundled manifest when Azure omits file attributes.</summary>
    /// <param name="manifest">Validated manifest matching the configured vector-store fingerprint.</param>
    public AzureOpenAIVectorStoreCorpusParser(DocumentManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (!string.Equals(manifest.SchemaVersion, ManifestLoader.SupportedSchemaVersion, StringComparison.Ordinal) || !string.Equals(manifest.SourceProvider, "Sefaria", StringComparison.Ordinal) || manifest.Documents is null || manifest.DocumentCount != manifest.Documents.Count)
        {
            throw new ArgumentException("Managed retrieval requires a complete supported Sefaria manifest.", nameof(manifest));
        }
        try
        {
            documents = manifest.Documents.ToDictionary(document => document.DocumentId, StringComparer.Ordinal);
        }
        catch (ArgumentException exception)
        {
            throw new ArgumentException("Managed retrieval manifest contains duplicate document IDs.", nameof(manifest), exception);
        }
    }

    /// <summary>Parses complete search records and rejects mismatched provenance.</summary>
    /// <param name="attributes">File-level attributes returned by Azure.</param>
    /// <param name="content">Search chunks returned by Azure.</param>
    /// <param name="expectedCorpusFingerprint">Configured immutable corpus fingerprint.</param>
    /// <returns>Unique full segments or explicitly identified excerpts.</returns>
    public IReadOnlyList<SourceSegment> Parse(IReadOnlyDictionary<string, string> attributes, IReadOnlyList<string> content, string expectedCorpusFingerprint)
    {
        ArgumentNullException.ThrowIfNull(attributes);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedCorpusFingerprint);
        ValidateFingerprint(expectedCorpusFingerprint);
        var segments = new Dictionary<string, SourceSegment>(StringComparer.Ordinal);
        foreach (var chunk in content)
        {
            if (string.IsNullOrEmpty(chunk))
            {
                continue;
            }

            foreach (var segment in ParseChunk(chunk, attributes, expectedCorpusFingerprint))
            {
                if (segments.TryGetValue(segment.SegmentId, out var existing) && existing != segment)
                {
                    throw new InvalidDataException($"Azure returned conflicting content for segment record '{segment.SegmentId}'.");
                }
                segments[segment.SegmentId] = segment;
            }
        }

        return segments.Values.OrderBy(segment => segment.DocumentOrdinal).ThenBy(segment => segment.ExcerptStart).ThenBy(segment => segment.SegmentId, StringComparer.Ordinal).ToArray();
    }

    private IEnumerable<SourceSegment> ParseChunk(string chunk, IReadOnlyDictionary<string, string> attributes, string expectedCorpusFingerprint)
    {
        var normalized = chunk.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var searchIndex = 0;
        while (searchIndex < normalized.Length)
        {
            var start = normalized.IndexOf(AzureOpenAIVectorStoreCorpusContract.StartMarker, searchIndex, StringComparison.Ordinal);
            if (start < 0)
            {
                yield break;
            }

            var metadataStart = start + AzureOpenAIVectorStoreCorpusContract.StartMarker.Length;
            if (metadataStart >= normalized.Length || normalized[metadataStart] != '\n')
            {
                searchIndex = metadataStart;
                continue;
            }
            metadataStart++;
            var metadataEnd = normalized.IndexOf('\n', metadataStart);
            if (metadataEnd < 0)
            {
                yield break;
            }

            var passageBoundary = $"\n{AzureOpenAIVectorStoreCorpusContract.PassageMarker}\n";
            var passageMarker = normalized.IndexOf(passageBoundary, metadataEnd, StringComparison.Ordinal);
            if (passageMarker < 0)
            {
                yield break;
            }
            var textStart = passageMarker + passageBoundary.Length;
            var endBoundary = $"\n{AzureOpenAIVectorStoreCorpusContract.EndMarker}";
            var textEnd = normalized.IndexOf(endBoundary, textStart, StringComparison.Ordinal);
            if (textEnd < 0)
            {
                yield break;
            }

            var metadataJson = normalized[metadataStart..metadataEnd];
            var text = normalized[textStart..textEnd];
            var envelope = DeserializeEnvelope(metadataJson);
            var provenance = ResolveProvenance(attributes, envelope, expectedCorpusFingerprint);
            ValidateEnvelope(envelope, provenance.DocumentId, text);
            yield return CreateSegment(envelope, provenance, text);
            searchIndex = textEnd + endBoundary.Length;
        }
    }

    private Provenance ResolveProvenance(IReadOnlyDictionary<string, string> attributes, SegmentEnvelope envelope, string expectedCorpusFingerprint)
    {
        if (documents is null)
        {
            return ParseProvenance(attributes, expectedCorpusFingerprint);
        }
        var markerIndex = envelope.OriginalSegmentId.LastIndexOf(":segment:", StringComparison.Ordinal);
        if (markerIndex <= 0)
        {
            throw new InvalidDataException($"Azure search record '{envelope.SegmentId}' has no stable document prefix.");
        }
        var documentId = envelope.OriginalSegmentId[..markerIndex];
        if (!documents.TryGetValue(documentId, out var document))
        {
            throw new InvalidDataException($"Azure search record '{envelope.SegmentId}' references an unknown manifest document.");
        }
        SourceLicenseCategory licenseCategory;
        try
        {
            licenseCategory = SourceLicensePolicy.Classify(document.License);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException($"Manifest license metadata is invalid for document '{document.DocumentId}'.", exception);
        }
        if (licenseCategory != document.LicenseCategory)
        {
            throw new InvalidDataException($"Manifest license metadata is inconsistent for document '{document.DocumentId}'.");
        }
        ValidateReturnedAttributes(attributes, document, expectedCorpusFingerprint);
        return new Provenance(
            document.DocumentId,
            document.FileTitle,
            document.HebrewTitle,
            document.FileLanguage,
            document.FileLanguageCode,
            document.Collection,
            document.Categories,
            document.VersionTitle,
            document.License,
            licenseCategory,
            document.AttributionUrl,
            document.FilePath,
            document.WorkKey,
            document.UsageNote);
    }

    private static void ValidateReturnedAttributes(IReadOnlyDictionary<string, string> attributes, ManifestDocument document, string expectedCorpusFingerprint)
    {
        ValidateOptionalAttribute(attributes, AzureOpenAIVectorStoreCorpusContract.CorpusFingerprintAttribute, expectedCorpusFingerprint);
        ValidateOptionalAttribute(attributes, AzureOpenAIVectorStoreCorpusContract.DocumentIdAttribute, document.DocumentId);
        ValidateOptionalAttribute(attributes, AzureOpenAIVectorStoreCorpusContract.TitleAttribute, document.FileTitle);
        ValidateOptionalAttribute(attributes, AzureOpenAIVectorStoreCorpusContract.HebrewTitleAttribute, document.HebrewTitle);
        ValidateOptionalAttribute(attributes, AzureOpenAIVectorStoreCorpusContract.LanguageAttribute, document.FileLanguage);
        ValidateOptionalAttribute(attributes, AzureOpenAIVectorStoreCorpusContract.LanguageCodeAttribute, document.FileLanguageCode);
        ValidateOptionalAttribute(attributes, AzureOpenAIVectorStoreCorpusContract.CollectionAttribute, document.Collection);
        ValidateOptionalAttribute(attributes, AzureOpenAIVectorStoreCorpusContract.CategoriesAttribute, JsonSerializer.Serialize(document.Categories));
        ValidateOptionalAttribute(attributes, AzureOpenAIVectorStoreCorpusContract.VersionAttribute, document.VersionTitle);
        ValidateOptionalAttribute(attributes, AzureOpenAIVectorStoreCorpusContract.LicenseAttribute, document.License);
        ValidateOptionalAttribute(attributes, AzureOpenAIVectorStoreCorpusContract.LicenseCategoryAttribute, document.LicenseCategory.ToString());
        ValidateOptionalAttribute(attributes, AzureOpenAIVectorStoreCorpusContract.SourceUrlAttribute, document.AttributionUrl);
        ValidateOptionalAttribute(attributes, AzureOpenAIVectorStoreCorpusContract.FilePathAttribute, document.FilePath);
        ValidateOptionalAttribute(attributes, AzureOpenAIVectorStoreCorpusContract.WorkKeyAttribute, document.WorkKey ?? string.Empty);
        ValidateOptionalAttribute(attributes, AzureOpenAIVectorStoreCorpusContract.UsageNoteAttribute, document.UsageNote ?? string.Empty);
        ValidateOptionalAttribute(attributes, AzureOpenAIVectorStoreCorpusContract.SourceProviderAttribute, "Sefaria");
    }

    private static void ValidateOptionalAttribute(IReadOnlyDictionary<string, string> attributes, string key, string expectedValue)
    {
        if (attributes.TryGetValue(key, out var actualValue) && !string.Equals(actualValue, expectedValue, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Azure vector-store attribute '{key}' conflicts with the trusted manifest.");
        }
    }

    private static SegmentEnvelope DeserializeEnvelope(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<SegmentEnvelope>(json, EnvelopeJsonOptions) ?? throw new InvalidDataException("Azure search record contains null segment metadata.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Azure search record contains invalid segment metadata.", exception);
        }
    }

    private static void ValidateEnvelope(SegmentEnvelope envelope, string documentId, string text)
    {
        if (envelope.DocumentOrdinal < 0 || envelope.WindowIndex < 0 || envelope.ExcerptStart < 0 || envelope.OriginalCharacterCount < 1)
        {
            throw new InvalidDataException($"Azure search record '{envelope.SegmentId}' contains invalid numeric metadata.");
        }
        var expectedOriginalId = $"{documentId}:segment:{envelope.DocumentOrdinal + 1:D8}";
        if (!string.Equals(envelope.OriginalSegmentId, expectedOriginalId, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Azure search record '{envelope.SegmentId}' does not match its document and ordinal.");
        }
        var expectedId = envelope.IsExcerpt ? $"{expectedOriginalId}:excerpt:{envelope.WindowIndex + 1:D4}" : expectedOriginalId;
        if (!string.Equals(envelope.SegmentId, expectedId, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Azure search record ID '{envelope.SegmentId}' is not stable for its source window.");
        }
        if (!string.Equals(envelope.LookupToken, AzureOpenAIVectorStoreCorpusFormatter.CreateLookupToken(documentId, envelope.DocumentOrdinal), StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Azure search record '{envelope.SegmentId}' has an invalid context lookup token.");
        }
        if (text.Length == 0 || envelope.ExcerptStart + text.Length > envelope.OriginalCharacterCount)
        {
            throw new InvalidDataException($"Azure search record '{envelope.SegmentId}' has invalid excerpt bounds.");
        }
        if (!envelope.IsExcerpt && (envelope.WindowIndex != 0 || envelope.ExcerptStart != 0 || envelope.OriginalCharacterCount != text.Length))
        {
            throw new InvalidDataException($"Azure full-segment record '{envelope.SegmentId}' contains excerpt metadata.");
        }
        if (string.IsNullOrWhiteSpace(envelope.CanonicalReference))
        {
            throw new InvalidDataException($"Azure search record '{envelope.SegmentId}' has no canonical reference.");
        }
    }

    private static Provenance ParseProvenance(IReadOnlyDictionary<string, string> attributes, string expectedCorpusFingerprint)
    {
        var fingerprint = GetRequired(attributes, AzureOpenAIVectorStoreCorpusContract.CorpusFingerprintAttribute);
        if (!string.Equals(fingerprint, expectedCorpusFingerprint, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Azure vector-store file fingerprint does not match the configured corpus.");
        }
        var sourceProvider = GetRequired(attributes, AzureOpenAIVectorStoreCorpusContract.SourceProviderAttribute);
        if (!string.Equals(sourceProvider, "Sefaria", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Unsupported vector-store source provider '{sourceProvider}'.");
        }

        var categoriesJson = GetRequired(attributes, AzureOpenAIVectorStoreCorpusContract.CategoriesAttribute);
        IReadOnlyList<string> categories;
        try
        {
            categories = JsonSerializer.Deserialize<string[]>(categoriesJson) ?? throw new InvalidDataException("Azure vector-store categories are null.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Azure vector-store categories are invalid JSON.", exception);
        }
        if (categories.Count == 0 || categories.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidDataException("Azure vector-store categories must contain nonempty values.");
        }

        var license = GetRequired(attributes, AzureOpenAIVectorStoreCorpusContract.LicenseAttribute);
        SourceLicenseCategory licenseCategory;
        try
        {
            licenseCategory = SourceLicensePolicy.Classify(license);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException("Azure vector-store file does not have an approved source license.", exception);
        }
        var categoryValue = GetRequired(attributes, AzureOpenAIVectorStoreCorpusContract.LicenseCategoryAttribute);
        if (!Enum.TryParse<SourceLicenseCategory>(categoryValue, false, out var recordedCategory) || recordedCategory != licenseCategory)
        {
            throw new InvalidDataException("Azure vector-store license metadata is inconsistent.");
        }

        return new Provenance(
            GetRequired(attributes, AzureOpenAIVectorStoreCorpusContract.DocumentIdAttribute),
            GetRequired(attributes, AzureOpenAIVectorStoreCorpusContract.TitleAttribute),
            GetRequired(attributes, AzureOpenAIVectorStoreCorpusContract.HebrewTitleAttribute),
            GetRequired(attributes, AzureOpenAIVectorStoreCorpusContract.LanguageAttribute),
            GetRequired(attributes, AzureOpenAIVectorStoreCorpusContract.LanguageCodeAttribute),
            GetRequired(attributes, AzureOpenAIVectorStoreCorpusContract.CollectionAttribute),
            categories.ToArray(),
            GetRequired(attributes, AzureOpenAIVectorStoreCorpusContract.VersionAttribute),
            license,
            licenseCategory,
            GetRequired(attributes, AzureOpenAIVectorStoreCorpusContract.SourceUrlAttribute),
            GetRequired(attributes, AzureOpenAIVectorStoreCorpusContract.FilePathAttribute),
            GetOptional(attributes, AzureOpenAIVectorStoreCorpusContract.WorkKeyAttribute),
            GetOptional(attributes, AzureOpenAIVectorStoreCorpusContract.UsageNoteAttribute));
    }

    private static SourceSegment CreateSegment(SegmentEnvelope envelope, Provenance provenance, string text) => new()
    {
        SegmentId = envelope.SegmentId,
        DocumentId = provenance.DocumentId,
        CanonicalReference = envelope.CanonicalReference,
        DocumentOrdinal = envelope.DocumentOrdinal,
        Text = text,
        Title = provenance.Title,
        HebrewTitle = provenance.HebrewTitle,
        Language = provenance.Language,
        LanguageCode = provenance.LanguageCode,
        Collection = provenance.Collection,
        Categories = provenance.Categories,
        Version = provenance.Version,
        License = provenance.License,
        LicenseCategory = provenance.LicenseCategory,
        SourceUrl = provenance.SourceUrl,
        FilePath = provenance.FilePath,
        WorkKey = provenance.WorkKey,
        UsageNote = provenance.UsageNote,
        IsExcerpt = envelope.IsExcerpt,
        OriginalSegmentId = envelope.IsExcerpt ? envelope.OriginalSegmentId : null,
        ExcerptStart = envelope.ExcerptStart,
        OriginalCharacterCount = envelope.OriginalCharacterCount,
    };

    private static string GetRequired(IReadOnlyDictionary<string, string> attributes, string key)
    {
        if (!attributes.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException($"Azure vector-store file is missing required attribute '{key}'.");
        }
        return value;
    }

    private static string? GetOptional(IReadOnlyDictionary<string, string> attributes, string key) => attributes.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    private static void ValidateFingerprint(string value)
    {
        if (value is not { Length: 64 } || value.Any(character => character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
        {
            throw new ArgumentException("Expected corpus fingerprint must be a lowercase SHA-256 value.", nameof(value));
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

    private sealed record Provenance(string DocumentId, string Title, string HebrewTitle, string Language, string LanguageCode, string Collection, IReadOnlyList<string> Categories, string Version, string License, SourceLicenseCategory LicenseCategory, string SourceUrl, string FilePath, string? WorkKey, string? UsageNote);
}
