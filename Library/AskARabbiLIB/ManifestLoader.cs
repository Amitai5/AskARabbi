using System.Text.Json;
using System.Text.Json.Serialization;
using AskARabbiLIB.Models;

namespace AskARabbiLIB;

/// <summary>Loads and validates AI-facing Sefaria document manifests.</summary>
public sealed class ManifestLoader
{
    /// <summary>Gets the only manifest schema version accepted by this library build.</summary>
    public const string SupportedSchemaVersion = "1.3";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        AllowTrailingCommas = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    /// <summary>Loads and validates a manifest from a file.</summary>
    /// <param name="manifestPath">Path to the JSON manifest.</param>
    /// <param name="cancellationToken">Token used to cancel asynchronous file reading.</param>
    /// <returns>The validated in-memory manifest.</returns>
    public async Task<DocumentManifest> LoadAsync(string manifestPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);
        await using var stream = new FileStream(manifestPath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return await LoadAsync(stream, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Loads and validates a manifest from a stream without disposing the caller-owned stream.</summary>
    /// <param name="manifestStream">Readable stream containing a JSON manifest.</param>
    /// <param name="cancellationToken">Token used to cancel asynchronous reading.</param>
    /// <returns>The validated in-memory manifest.</returns>
    public async Task<DocumentManifest> LoadAsync(Stream manifestStream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifestStream);
        if (!manifestStream.CanRead)
        {
            throw new ArgumentException("The manifest stream must be readable.", nameof(manifestStream));
        }

        DocumentManifest? manifest;
        try
        {
            manifest = await JsonSerializer.DeserializeAsync<DocumentManifest>(manifestStream, SerializerOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The document manifest contains invalid JSON or does not match the expected schema.", exception);
        }

        if (manifest is null)
        {
            throw new InvalidDataException("The document manifest is empty.");
        }

        ValidateManifest(manifest);
        return CreateSnapshot(manifest);
    }

    private static void ValidateManifest(DocumentManifest manifest)
    {
        RequireString(manifest.SchemaVersion, "schemaVersion");
        if (!string.Equals(manifest.SchemaVersion, SupportedSchemaVersion, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Unsupported manifest schema version '{manifest.SchemaVersion}'. Expected '{SupportedSchemaVersion}'.");
        }

        RequireString(manifest.SourceProvider, "sourceProvider");
        RequireString(manifest.FilePathBase, "filePathBase");
        RequireString(manifest.Description, "description");
        if (manifest.GeneratedAtUtc == default)
        {
            throw new InvalidDataException("Manifest field 'generatedAtUtc' is required.");
        }
        if (manifest.GeneratedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new InvalidDataException("Manifest field 'generatedAtUtc' must use UTC (offset +00:00).");
        }
        if (manifest.DocumentCount < 0)
        {
            throw new InvalidDataException("Manifest field 'documentCount' cannot be negative.");
        }
        if (manifest.Documents is null)
        {
            throw new InvalidDataException("Manifest field 'documents' is required.");
        }
        if (manifest.DocumentCount != manifest.Documents.Count)
        {
            throw new InvalidDataException($"Manifest documentCount is {manifest.DocumentCount}, but documents contains {manifest.Documents.Count} entries.");
        }
        if (manifest.SourceManifests is null)
        {
            throw new InvalidDataException("Manifest field 'sourceManifests' is required.");
        }

        ValidateSourceManifests(manifest.SourceManifests);
        var documentIds = new HashSet<string>(StringComparer.Ordinal);
        var normalizedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rawPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < manifest.Documents.Count; index++)
        {
            var document = manifest.Documents[index] ?? throw new InvalidDataException($"Manifest document at index {index} is null.");
            ValidateDocument(document, index);
            if (!documentIds.Add(document.DocumentId))
            {
                throw new InvalidDataException($"Manifest contains duplicate documentId '{document.DocumentId}'.");
            }
            if (!normalizedPaths.Add(document.FilePath))
            {
                throw new InvalidDataException($"Manifest contains duplicate filePath '{document.FilePath}'.");
            }
            if (!rawPaths.Add(document.RawFilePath))
            {
                throw new InvalidDataException($"Manifest contains duplicate rawFilePath '{document.RawFilePath}'.");
            }
        }
    }

    private static void ValidateSourceManifests(SourceManifestReferences references)
    {
        RequireString(references.Raw, "sourceManifests.raw");
        RequireSha256(references.RawSha256, "sourceManifests.rawSha256");
        RequireString(references.Normalized, "sourceManifests.normalized");
        RequireSha256(references.NormalizedSha256, "sourceManifests.normalizedSha256");
    }

    private static void ValidateDocument(ManifestDocument document, int index)
    {
        var prefix = $"documents[{index}]";
        RequireString(document.DocumentId, $"{prefix}.documentId");
        RequireString(document.FilePath, $"{prefix}.filePath");
        RequireString(document.FileDescription, $"{prefix}.fileDescription");
        RequireString(document.FileLanguage, $"{prefix}.fileLanguage");
        RequireString(document.FileTitle, $"{prefix}.fileTitle");
        RequireString(document.FileLanguageCode, $"{prefix}.fileLanguageCode");
        RequireString(document.Collection, $"{prefix}.collection");
        RequireString(document.HebrewTitle, $"{prefix}.hebrewTitle");
        RequireString(document.VersionTitle, $"{prefix}.versionTitle");
        RequireString(document.License, $"{prefix}.license");
        RequireString(document.LicenseStatus, $"{prefix}.licenseStatus");
        if (!string.Equals(document.LicenseStatus, "permissive", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Manifest field '{prefix}.licenseStatus' must be 'permissive'.");
        }
        RequireString(document.SourceUrl, $"{prefix}.sourceUrl");
        RequireString(document.AttributionUrl, $"{prefix}.attributionUrl");
        RequireString(document.RawFilePath, $"{prefix}.rawFilePath");
        RequireSha256(document.RawSha256, $"{prefix}.rawSha256");
        RequireSha256(document.Sha256, $"{prefix}.sha256");
        var expectedDocumentId = $"sefaria:{document.RawSha256.ToLowerInvariant()}";
        if (!string.Equals(document.DocumentId, expectedDocumentId, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Manifest field '{prefix}.documentId' must equal '{expectedDocumentId}'.");
        }
        SourceLicenseCategory expectedCategory;
        try
        {
            expectedCategory = SourceLicensePolicy.Classify(document.License);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException($"Manifest field '{prefix}.license' is unsupported.", exception);
        }
        if (document.LicenseCategory != expectedCategory)
        {
            throw new InvalidDataException($"Manifest field '{prefix}.licenseCategory' does not match license '{document.License}'.");
        }
        if (document.RequiresAttribution != SourceLicensePolicy.RequiresAttribution(expectedCategory))
        {
            throw new InvalidDataException($"Manifest field '{prefix}.requiresAttribution' does not match license category '{expectedCategory}'.");
        }
        if (document.RequiresShareAlike != SourceLicensePolicy.RequiresShareAlike(expectedCategory))
        {
            throw new InvalidDataException($"Manifest field '{prefix}.requiresShareAlike' does not match license category '{expectedCategory}'.");
        }
        RequireHttpUrl(document.SourceUrl, $"{prefix}.sourceUrl");
        RequireHttpUrl(document.AttributionUrl, $"{prefix}.attributionUrl");
        if (document.Categories is null || document.Categories.Count == 0 || document.Categories.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidDataException($"Manifest field '{prefix}.categories' must contain nonempty values.");
        }
        if ((document.WorkKey is null) != (document.UsageNote is null))
        {
            throw new InvalidDataException($"Manifest fields '{prefix}.workKey' and '{prefix}.usageNote' must both be present or both be null.");
        }
        if (document.WorkKey is not null)
        {
            RequireString(document.WorkKey, $"{prefix}.workKey");
            RequireString(document.UsageNote, $"{prefix}.usageNote");
        }
        if (document.SegmentCount < 0)
        {
            throw new InvalidDataException($"Manifest field '{prefix}.segmentCount' cannot be negative.");
        }
        if (document.FileSizeBytes < 0)
        {
            throw new InvalidDataException($"Manifest field '{prefix}.fileSizeBytes' cannot be negative.");
        }
        if ((document.FirstReference is null) != (document.LastReference is null))
        {
            throw new InvalidDataException($"Manifest fields '{prefix}.firstReference' and '{prefix}.lastReference' must both be present or both be null.");
        }
    }

    private static DocumentManifest CreateSnapshot(DocumentManifest manifest)
    {
        var documents = manifest.Documents.Select(document => document with { Categories = document.Categories.ToArray() }).ToArray();
        return manifest with { Documents = documents };
    }

    private static void RequireString(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException($"Manifest field '{fieldName}' is required.");
        }
    }

    private static void RequireSha256(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 64 || value.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new InvalidDataException($"Manifest field '{fieldName}' must be a 64-character SHA-256 value.");
        }
    }

    private static void RequireHttpUrl(string value, string fieldName)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            throw new InvalidDataException($"Manifest field '{fieldName}' must be an absolute HTTP or HTTPS URL.");
        }
    }
}
