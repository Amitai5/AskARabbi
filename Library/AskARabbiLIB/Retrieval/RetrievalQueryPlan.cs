namespace AskARabbiLIB.Retrieval;

internal sealed record RetrievalQueryPlan(IReadOnlyList<RetrievalConcept> Concepts, RetrievalConcept? TopicAnchor)
{
    internal IReadOnlyList<RetrievalConcept> SupportingConcepts => TopicAnchor is null ? Concepts : Concepts.Where(concept => !ReferenceEquals(concept, TopicAnchor)).ToArray();
}
