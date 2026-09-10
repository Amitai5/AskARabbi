namespace AskARabbiLIB.AI;

/// <summary>Admits provider requests and durably observes usage before responses are interpreted.</summary>
public interface IAIUsageObserver
{
    /// <summary>Checks whether another paid provider request is allowed.</summary>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>The admission task.</returns>
    Task BeforeRequestAsync(CancellationToken cancellationToken = default);

    /// <summary>Accounts for one provider response, even if its output is invalid or incomplete.</summary>
    /// <param name="responseId">Provider response identifier for duplicate detection.</param>
    /// <param name="usage">Provider-reported token counts.</param>
    /// <returns>The durable accounting task, independent of browser cancellation.</returns>
    Task RecordAsync(string? responseId, AIUsage usage);
}
