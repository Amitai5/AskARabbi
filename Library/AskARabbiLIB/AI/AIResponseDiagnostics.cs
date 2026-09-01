namespace AskARabbiLIB.AI;

/// <summary>Reports provider diagnostics without retaining prompts or response bodies.</summary>
/// <param name="ResponseId">Provider response identifier when available.</param>
/// <param name="Model">Model or deployment reported for the request.</param>
/// <param name="Usage">Provider token usage when available.</param>
/// <param name="Latency">Total latency across all attempts.</param>
/// <param name="Attempts">Number of provider attempts.</param>
/// <param name="ProviderStatus">Final typed provider status after transport and response validation.</param>
/// <param name="CompletionReason">Safe provider completion or failure category when available.</param>
public sealed record AIResponseDiagnostics(string? ResponseId, string Model, AIUsage? Usage, TimeSpan Latency, int Attempts, AIEngineStatus ProviderStatus = AIEngineStatus.Success, string? CompletionReason = null);
