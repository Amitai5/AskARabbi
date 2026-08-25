using System.Text.Json.Serialization;

namespace AskARabbiLIB.Models;

/// <summary>Describes one normalized document and its original Sefaria JSON source.</summary>
public sealed record ManifestDocument
{
    [JsonPropertyName("documentId")]
    public required string DocumentId { get; init; }

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

    [JsonPropertyName("workKey")]
    public string? WorkKey { get; init; }

    [JsonPropertyName("usageNote")]
    public string? UsageNote { get; init; }

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
    public required string License { get; init; }

    [JsonPropertyName("licenseCategory")]
    public required SourceLicenseCategory LicenseCategory { get; init; }

    [JsonPropertyName("requiresAttribution")]
    public required bool RequiresAttribution { get; init; }

    [JsonPropertyName("requiresShareAlike")]
    public required bool RequiresShareAlike { get; init; }

    [JsonPropertyName("licenseStatus")]
    public required string LicenseStatus { get; init; }

    [JsonPropertyName("sourceUrl")]
    public required string SourceUrl { get; init; }

    [JsonPropertyName("attributionUrl")]
    public required string AttributionUrl { get; init; }

    [JsonPropertyName("rawFilePath")]
    public required string RawFilePath { get; init; }

    [JsonPropertyName("rawSha256")]
    public required string RawSha256 { get; init; }

    [JsonPropertyName("sha256")]
    public required string Sha256 { get; init; }

    [JsonPropertyName("fileSizeBytes")]
    public required long FileSizeBytes { get; init; }
}
