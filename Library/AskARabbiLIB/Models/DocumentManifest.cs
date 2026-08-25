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
