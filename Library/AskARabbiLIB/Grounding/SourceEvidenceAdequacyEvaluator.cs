using AskARabbiLIB.Retrieval;

namespace AskARabbiLIB.Grounding;

internal static class SourceEvidenceAdequacyEvaluator
{
    internal static SourceEvidenceAdequacyResult Evaluate(string retrievalText, IReadOnlyList<SourceRetrievalHit> hits)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(retrievalText);
        ArgumentNullException.ThrowIfNull(hits);
        if (hits.Count == 0)
        {
            return SourceEvidenceAdequacyResult.Insufficient("No relevant passages were found in the selected approved sources. The model was not called.", []);
        }

        var plan = RetrievalQueryPlanner.Plan(retrievalText);
        if (plan.Concepts.Count == 0)
        {
            return SourceEvidenceAdequacyResult.Insufficient("The question did not contain enough searchable subject matter to identify grounded evidence. The model was not called.", []);
        }

        var scoredHits = hits.Select(hit => Score(hit, plan)).ToArray();
        IReadOnlyList<ScoredSourceHit> relevant;
        if (plan.TopicAnchor is not null)
        {
            relevant = scoredHits.Where(hit => hit.MatchesTopicAnchor && (plan.SupportingConcepts.Count == 0 || hit.SupportMatches > 0)).ToArray();
            var matchedSupports = relevant.SelectMany(hit => hit.MatchedSupportKeys).Distinct(StringComparer.Ordinal).Count();
            var requiredSupports = Math.Min(2, plan.SupportingConcepts.Count);
            if (relevant.Count == 0 || matchedSupports < requiredSupports)
            {
                return SourceEvidenceAdequacyResult.Insufficient($"The retrieved passages did not connect the question's '{plan.TopicAnchor.Key}' topic to enough of its supporting concepts. The model was not called.", hits);
            }
        }
        else
        {
            var requiredMatches = Math.Min(2, plan.Concepts.Count);
            relevant = scoredHits.Where(hit => hit.TotalMatches >= requiredMatches).ToArray();
            if (relevant.Count == 0)
            {
                return SourceEvidenceAdequacyResult.Insufficient("The retrieved passages matched isolated words but did not provide enough topic-relevant evidence to answer the question. The model was not called.", hits);
            }
        }

        var relevantIds = relevant.Select(hit => hit.Hit.Segment.SegmentId).ToHashSet(StringComparer.Ordinal);
        var ordered = relevant
            .OrderByDescending(hit => hit.TotalMatches)
            .ThenByDescending(hit => hit.Hit.Score)
            .Select(hit => hit.Hit)
            .Concat(hits.Where(hit => !relevantIds.Contains(hit.Segment.SegmentId)))
            .ToArray();
        return SourceEvidenceAdequacyResult.Adequate(ordered);
    }

    private static ScoredSourceHit Score(SourceRetrievalHit hit, RetrievalQueryPlan plan)
    {
        var searchableTokens = RetrievalQueryPlanner.CreateSearchableTokens(hit.Segment);
        var matched = plan.Concepts.Where(concept => RetrievalQueryPlanner.Matches(concept, searchableTokens)).ToArray();
        var matchedSupportKeys = matched.Where(concept => !concept.IsTopicAnchor).Select(concept => concept.Key).ToArray();
        return new ScoredSourceHit(hit, plan.TopicAnchor is not null && matched.Any(concept => concept.Key == plan.TopicAnchor.Key), matched.Length, matchedSupportKeys);
    }

    private sealed record ScoredSourceHit(SourceRetrievalHit Hit, bool MatchesTopicAnchor, int TotalMatches, IReadOnlyList<string> MatchedSupportKeys)
    {
        internal int SupportMatches => MatchedSupportKeys.Count;
    }
}
