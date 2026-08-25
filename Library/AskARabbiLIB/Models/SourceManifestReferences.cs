using System.Text.Json.Serialization;

namespace AskARabbiLIB.Models;

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
