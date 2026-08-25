using AskARabbiLIB.Retrieval;

namespace AskARabbiLIB.Grounding;

/// <summary>Represents one bounded source segment exposed to the model under an opaque ID.</summary>
/// <param name="EvidenceId">Request-local opaque evidence identifier.</param>
/// <param name="Source">Trusted source segment and provenance.</param>
/// <param name="PresentedText">Exact full text or explicitly marked excerpt supplied to the model.</param>
/// <param name="IsExcerpt">Whether the presented text is an explicit excerpt.</param>
/// <param name="OriginalCharacterCount">Character count before excerpting.</param>
public sealed record EvidenceItem(string EvidenceId, SourceSegment Source, string PresentedText, bool IsExcerpt, int OriginalCharacterCount);
