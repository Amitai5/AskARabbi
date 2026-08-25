namespace AskARabbiLIB.AI;

internal sealed record AITransportResult(AIEngineStatus Status, string? OutputJson, string? ErrorMessage, string? ResponseId, string Model, AIUsage? Usage, bool Retryable);
