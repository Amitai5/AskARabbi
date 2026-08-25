namespace AskARabbiLIB.Models;

/// <summary>Represents one ranked document match.</summary>
/// <param name="Document">Matched manifest document.</param>
/// <param name="Score">Deterministic metadata relevance score.</param>
/// <param name="MatchedFields">Names of metadata fields that matched.</param>
public sealed record ManifestSearchHit(ManifestDocument Document, int Score, IReadOnlyList<string> MatchedFields);
