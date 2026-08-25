namespace AskARabbiLIB.Retrieval;

internal sealed record RetrievalConceptDefinition(string Key, string[] Tokens, int Priority, bool IsTopicAnchor);
