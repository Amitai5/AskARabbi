namespace AskARabbiLIB.Grounding;

/// <summary>Represents a validated source-backed educational answer.</summary>
public sealed record GroundedAnswer(IReadOnlyList<GroundedClaim> Claims, IReadOnlyList<GroundedDisagreement> Disagreements, IReadOnlyList<string> Limitations, string? ClarifyingQuestion, bool HumanGuidanceRecommended, IReadOnlyList<SourceCitation> Citations)
{
    /// <summary>Gets the optional AI-generated title for the first validated response.</summary>
    public string? SuggestedConversationTitle { get; init; }

    /// <summary>Gets the language of this answer, including application-rendered transitions.</summary>
    public string ResponseLanguage { get; init; } = "English";

    /// <summary>Gets the requested quotation language for a truthful edition-availability notice.</summary>
    public string? QuotationLanguage { get; init; }

    /// <summary>Gets the application-controlled notice appended after the grounded answer.</summary>
    public required string InterpretiveNotice { get; init; }
}
