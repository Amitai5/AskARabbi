namespace AskARabbiLIB.DvarTorah;

/// <summary>Reports a stable, non-sensitive stage code when weekly content generation fails closed.</summary>
public sealed class WeeklyDvarTorahGenerationException : InvalidOperationException
{
    internal WeeklyDvarTorahGenerationException(string failureCode, string message) : base(message)
    {
        FailureCode = failureCode;
    }

    /// <summary>Gets the bounded stage code safe for logs and persisted failure state.</summary>
    public string FailureCode { get; }
}
