namespace AskARabbiLIB.Grounding;

/// <summary>Identifies a grounded-answer orchestration outcome.</summary>
public enum GroundedAnswerStatus
{
    Success,
    InsufficientEvidence,
    AuthenticationFailed,
    ValidationFailed,
    AIUnavailable,
}
