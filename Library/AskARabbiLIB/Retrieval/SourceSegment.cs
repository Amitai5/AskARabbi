using AskARabbiLIB.Models;

namespace AskARabbiLIB.Retrieval;

/// <summary>Represents one citation-addressable segment from an approved source document.</summary>
public sealed record SourceSegment
{
    public required string SegmentId { get; init; }

    public required string DocumentId { get; init; }

    public required string CanonicalReference { get; init; }

    public required int DocumentOrdinal { get; init; }

    public required string Text { get; init; }

    public required string Title { get; init; }

    public required string HebrewTitle { get; init; }

    public required string Language { get; init; }

    public required string LanguageCode { get; init; }

    public required string Collection { get; init; }

    public required IReadOnlyList<string> Categories { get; init; }

    public required string Version { get; init; }

    public required string License { get; init; }

    public required SourceLicenseCategory LicenseCategory { get; init; }

    public required string SourceUrl { get; init; }

    public required string FilePath { get; init; }

    public string? WorkKey { get; init; }

    public string? UsageNote { get; init; }
}
