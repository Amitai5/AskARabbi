using AskARabbiLIB.Grounding;
using AskARabbiLIB.Retrieval;

namespace AskARabbiLIB.AI.Tools;

/// <summary>Allows bounded research in the approved corpus without changing the user's source restrictions.</summary>
public sealed class SourceResearchAITools
{
    private const int MaximumSources = 3;
    private const int ExcerptLength = 2_400;
    private readonly ISourceRetriever retriever;
    private readonly ICanonicalSourceReader reader;

    /// <summary>Creates read-only religious-source research capabilities.</summary>
    /// <param name="retriever">Approved-corpus search, including normal token accounting.</param>
    /// <param name="reader">Verified canonical text reader.</param>
    public SourceResearchAITools(ISourceRetriever retriever, ICanonicalSourceReader reader)
    {
        this.retriever = retriever ?? throw new ArgumentNullException(nameof(retriever));
        this.reader = reader ?? throw new ArgumentNullException(nameof(reader));
    }

    /// <summary>Searches related terminology when the initial passages do not answer the question.</summary>
    /// <param name="query">A focused query of up to 400 characters.</param>
    /// <param name="context">Trusted source restrictions for this conversation.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>Bounded original passages with references, or an explicit no-match result.</returns>
    [AITool("search_source_passages", "Search the approved religious corpus again when initial passages miss the actual question. Use specific concepts and alternate names, Hebrew/Aramaic terms, or transliterations; do not search only the holiday or broad topic. Your knowledge may suggest searches, never supply unsupported answers. Read exact references with read_source_passage for complete context or a preferred translation. Source filters cannot be changed. No match is not proof that a custom does not exist. Never mention this process in the answer.")]
    public async Task<AIToolExecutionResult> SearchAsync([AIToolParameter("Specific source-search terms, maximum 400 characters. Include the object/custom and the relationship being asked about.")] string query, AIToolExecutionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        if (query.Length > 400)
        {
            return AIToolExecutionResult.Failure("Use a focused search query of at most 400 characters.");
        }
        var filters = RequireFilters(context);
        var hits = await retriever.SearchAsync(filters with { QueryText = query.Trim(), ExactCanonicalReference = null, CandidateLimit = 12 }, cancellationToken).ConfigureAwait(false);
        var adequacy = SourceEvidenceAdequacyEvaluator.Evaluate(query, hits);
        var originalQuestionAdequacy = string.IsNullOrWhiteSpace(filters.QueryText) ? adequacy : SourceEvidenceAdequacyEvaluator.Evaluate(filters.QueryText, hits);
        if (!adequacy.IsAdequate || !originalQuestionAdequacy.IsAdequate)
        {
            return AIToolExecutionResult.Failure("No passages directly addressed the original question within the enabled sources. Read a likely exact canonical reference now rather than broadening the topic. Unrelated matches do not prove absence of an answer.");
        }
        var sources = originalQuestionAdequacy.OrderedHits.DistinctBy(hit => hit.Segment.CanonicalReference).Take(MaximumSources).Select(hit => hit.Segment).ToArray();
        return CreateResult(sources, 0);
    }

    /// <summary>Reads original context or a preferred translation at an exact reference.</summary>
    /// <param name="reference">Canonical reference, never a URL or path.</param>
    /// <param name="context">Trusted source restrictions and preferred language order.</param>
    /// <param name="offset">Character offset within a single passage for continuation.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>Citable passages and continuation metadata, or an explicit unavailable result.</returns>
    [AITool("read_source_passage", "Read an exact canonical religious reference or short range from the approved corpus, including references suggested by your knowledge. The text must actually be returned before you can use it as evidence. Respects enabled sources and quotation language. Use returned nextOffset with the single canonicalReference to continue a long passage. This is not a web browser; never supply URLs or file paths.")]
    public async Task<AIToolExecutionResult> ReadAsync([AIToolParameter("Exact canonical reference, for example Shulchan Arukh, Orach Chayim 583:1; maximum 200 characters.")] string reference, AIToolExecutionContext context, [AIToolParameter("Zero for the beginning; use a returned nextOffset with its single canonicalReference to continue.")] int offset = 0, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        var filters = RequireFilters(context);
        if (reference.Length > 200 || reference.IndexOfAny(['/', '\\', '\r', '\n']) >= 0 || offset < 0 || !CanonicalReferenceRange.TryParse(reference, out _))
        {
            return AIToolExecutionResult.Failure("Use a valid canonical reference and a nonnegative character offset.");
        }
        var sources = await reader.ReadAsync(reference.Trim(), filters, cancellationToken).ConfigureAwait(false);
        if (sources.Count == 0 || offset > 0 && sources.Count != 1 || sources.Any(source => offset >= source.Text.Length))
        {
            return AIToolExecutionResult.Failure("The reference or offset is unavailable within the enabled sources. Try another relevant reference; do not infer that the practice does not exist.");
        }
        return CreateResult(sources, offset);
    }

    private static SourceRetrievalQuery RequireFilters(AIToolExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.SourceFilters ?? throw new InvalidOperationException("Religious-source research requires a trusted conversation source scope.");
    }

    private static AIToolExecutionResult CreateResult(IReadOnlyList<SourceSegment> sources, int offset)
    {
        var originals = sources.Take(MaximumSources).ToArray();
        var excerpts = originals.Select(source => Excerpt(source, offset)).ToArray();
        return AIToolExecutionResult.FromSources(new
        {
            passages = originals.Select((source, index) => new
            {
                source.CanonicalReference,
                source.Language,
                excerptStart = excerpts[index].ExcerptStart,
                nextOffset = excerpts[index].ExcerptStart + excerpts[index].Text.Length < Math.Max(source.OriginalCharacterCount, source.ExcerptStart + source.Text.Length) ? excerpts[index].ExcerptStart + excerpts[index].Text.Length : (int?)null,
            }),
            nextReference = sources.Skip(MaximumSources).FirstOrDefault()?.CanonicalReference,
        }, excerpts);
    }

    private static SourceSegment Excerpt(SourceSegment source, int offset)
    {
        if (offset > 0 && char.IsLowSurrogate(source.Text[offset]))
        {
            offset--;
        }
        var end = Math.Min(source.Text.Length, offset + ExcerptLength);
        if (end < source.Text.Length && char.IsHighSurrogate(source.Text[end - 1]))
        {
            end--;
        }
        return source with
        {
            Text = source.Text[offset..end],
            IsExcerpt = source.IsExcerpt || offset != 0 || end != source.Text.Length,
            ExcerptStart = source.ExcerptStart + offset,
            OriginalCharacterCount = Math.Max(source.OriginalCharacterCount, source.Text.Length),
        };
    }
}
