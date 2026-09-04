using AskARabbiLIB.AI.Tools;

namespace AskARabbiLIB.AI;

internal sealed record AITransportRequest(IReadOnlyList<AIMessage> Messages, string SchemaName, BinaryData JsonSchema, string Model, int MaximumOutputTokens, AIReasoningEffort ReasoningEffort, AIToolExecutionSession? ToolSession = null, AIServiceTier ServiceTier = AIServiceTier.Auto);
