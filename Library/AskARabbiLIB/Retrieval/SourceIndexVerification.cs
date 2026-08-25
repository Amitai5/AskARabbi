namespace AskARabbiLIB.Retrieval;

/// <summary>Describes whether a segment index matches the current corpus.</summary>
/// <param name="IsValid">Whether the index passed schema, fingerprint, count, and integrity checks.</param>
/// <param name="Message">Human-readable verification result.</param>
/// <param name="Statistics">Index statistics when verification could read them.</param>
public sealed record SourceIndexVerification(bool IsValid, string Message, SourceIndexStatistics? Statistics);
