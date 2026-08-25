namespace AskARabbiLIB.AI;

/// <summary>Generates provider-neutral structured responses from text-only messages.</summary>
public interface IAIEngine
{
    /// <summary>Generates and deserializes a response constrained by a strict JSON schema.</summary>
    /// <typeparam name="T">Structured response type.</typeparam>
    /// <param name="messages">Ordered text-only prompt messages.</param>
    /// <param name="schemaName">Stable JSON schema name.</param>
    /// <param name="jsonSchema">Strict JSON schema accepted by the provider.</param>
    /// <param name="cancellationToken">Token propagated to authentication and generation.</param>
    /// <returns>A typed success or explicit failure status.</returns>
    Task<AIEngineResult<T>> GenerateStructuredAsync<T>(IReadOnlyList<AIMessage> messages, string schemaName, BinaryData jsonSchema, CancellationToken cancellationToken = default);
}
