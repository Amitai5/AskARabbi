namespace AskARabbiLIB.Retrieval;

internal sealed record RetrievalConcept(string Key, IReadOnlyList<string> Tokens, bool IsTopicAnchor);
