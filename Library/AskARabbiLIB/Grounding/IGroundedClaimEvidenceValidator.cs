namespace AskARabbiLIB.Grounding;

/// <summary>Audits draft claims against the exact evidence they cite.</summary>
internal interface IGroundedClaimEvidenceValidator
{
    /// <summary>Validates relevance and support for every sourced draft statement.</summary>
    /// <param name="questionContext">Current question and bounded conversational context.</param>
    /// <param name="draft">Structured answer draft to audit.</param>
    /// <param name="packet">Trusted evidence available to the draft.</param>
    /// <param name="cancellationToken">Token used to cancel validation.</param>
    /// <returns>The support audit result and provider diagnostics.</returns>
    Task<ClaimEvidenceValidationResult> ValidateAsync(string questionContext, GroundedAnswerDraft draft, EvidencePacket packet, CancellationToken cancellationToken = default);
}
