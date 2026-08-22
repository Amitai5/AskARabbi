using System.Text.Json.Serialization;

namespace AskARabbiLIB.Models;

/// <summary>Represents the complete AI-facing Sefaria document manifest.</summary>
public sealed record DocumentManifest
{
    [JsonPropertyName("schemaVersion")]
    public required string SchemaVersion { get; init; }

    [JsonPropertyName("sourceProvider")]
    public required string SourceProvider { get; init; }

    [JsonPropertyName("generatedAtUtc")]
    public required DateTimeOffset GeneratedAtUtc { get; init; }

    [JsonPropertyName("filePathBase")]
    public required string FilePathBase { get; init; }

    [JsonPropertyName("description")]
    public required string Description { get; init; }

    [JsonPropertyName("documentCount")]
    public required int DocumentCount { get; init; }

    [JsonPropertyName("sourceManifests")]
    public required SourceManifestReferences SourceManifests { get; init; }

    [JsonPropertyName("documents")]
    public required IReadOnlyList<ManifestDocument> Documents { get; init; }
}

/// <summary>Identifies the raw and normalized manifests used to build the document catalog.</summary>
public sealed record SourceManifestReferences
{
    [JsonPropertyName("raw")]
    public required string Raw { get; init; }

    [JsonPropertyName("rawSha256")]
    public required string RawSha256 { get; init; }

    [JsonPropertyName("normalized")]
    public required string Normalized { get; init; }

    [JsonPropertyName("normalizedSha256")]
    public required string NormalizedSha256 { get; init; }
}

/// <summary>Describes one normalized document and its original Sefaria JSON source.</summary>
public sealed record ManifestDocument
{
    [JsonPropertyName("filePath")]
    public required string FilePath { get; init; }

    [JsonPropertyName("fileDescription")]
    public required string FileDescription { get; init; }

    [JsonPropertyName("fileLanguage")]
    public required string FileLanguage { get; init; }

    [JsonPropertyName("fileTitle")]
    public required string FileTitle { get; init; }

    [JsonPropertyName("fileLanguageCode")]
    public required string FileLanguageCode { get; init; }

    [JsonPropertyName("collection")]
    public required string Collection { get; init; }

    [JsonPropertyName("categories")]
    public required IReadOnlyList<string> Categories { get; init; }

    [JsonPropertyName("hebrewTitle")]
    public required string HebrewTitle { get; init; }

    [JsonPropertyName("versionTitle")]
    public required string VersionTitle { get; init; }

    [JsonPropertyName("versionSource")]
    public string? VersionSource { get; init; }

    [JsonPropertyName("firstReference")]
    public string? FirstReference { get; init; }

    [JsonPropertyName("lastReference")]
    public string? LastReference { get; init; }

    [JsonPropertyName("segmentCount")]
    public required int SegmentCount { get; init; }

    [JsonPropertyName("license")]
    public string? License { get; init; }

    [JsonPropertyName("licenseStatus")]
    public required string LicenseStatus { get; init; }

    [JsonPropertyName("sourceUrl")]
    public required string SourceUrl { get; init; }

    [JsonPropertyName("rawFilePath")]
    public required string RawFilePath { get; init; }

    [JsonPropertyName("rawSha256")]
    public required string RawSha256 { get; init; }

    [JsonPropertyName("sha256")]
    public required string Sha256 { get; init; }

    [JsonPropertyName("fileSizeBytes")]
    public required long FileSizeBytes { get; init; }
}
