namespace AskARabbiLIB.AI;

internal sealed record AITransportRequest(IReadOnlyList<AIMessage> Messages, string SchemaName, BinaryData JsonSchema, string Model, int MaximumOutputTokens, AIReasoningEffort ReasoningEffort);
