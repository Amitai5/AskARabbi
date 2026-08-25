namespace AskARabbiLIB.AI;

/// <summary>Reports token usage returned by the AI provider.</summary>
/// <param name="InputTokens">Number of input tokens reported by the provider.</param>
/// <param name="OutputTokens">Number of output tokens reported by the provider.</param>
/// <param name="TotalTokens">Total number of tokens reported by the provider.</param>
public sealed record AIUsage(int InputTokens, int OutputTokens, int TotalTokens);
