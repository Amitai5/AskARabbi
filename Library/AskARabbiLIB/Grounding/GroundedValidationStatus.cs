namespace AskARabbiLIB.Grounding;

/// <summary>Identifies the final validation state of a grounded answer.</summary>
public enum GroundedValidationStatus
{
    NotRun,
    Passed,
    Repaired,
    Failed,
}
