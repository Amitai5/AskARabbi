namespace AskARabbiLIB.AI;

internal interface IAIResponseTransport
{
    /// <summary>Sends one structured-output request to the configured AI provider.</summary>
    /// <param name="request">Provider-neutral request details.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <returns>The provider result and diagnostics.</returns>
    Task<AITransportResult> SendAsync(AITransportRequest request, CancellationToken cancellationToken);
}
