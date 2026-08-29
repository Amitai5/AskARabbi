using AskARabbiLIB.Retrieval;
using AskARabbiLIB.Search;

namespace AskARabbiLIB.Grounding;

internal sealed class EvidencePacketBuilder
{
    private readonly ISourceRetriever retriever;
    private readonly GroundedAnswerOptions options;

    internal EvidencePacketBuilder(ISourceRetriever retriever, GroundedAnswerOptions options)
    {
        ArgumentNullException.ThrowIfNull(retriever);
        ArgumentNullException.ThrowIfNull(options);
        this.retriever = retriever;
        this.options = options;
    }

    internal async Task<EvidencePacket> BuildAsync(IReadOnlyList<SourceRetrievalHit> hits, GroundedQuestion question, CancellationToken cancellationToken)
    {
        var items = new List<EvidenceItem>(options.MaximumEvidenceSegments);
        var seenSegments = new HashSet<string>(StringComparer.Ordinal);
        var documentCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var characterCount = 0;
        var enhancedHits = 0;

        foreach (var hit in hits)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TryAdd(hit.Segment, question.Question, items, seenSegments, documentCounts, ref characterCount);
            if (items.Count >= options.MaximumEvidenceSegments || characterCount >= options.MaximumEvidenceCharacters)
            {
                break;
            }

            if (enhancedHits >= 4)
            {
                continue;
            }
            enhancedHits++;

            var referenceMatches = await retriever.SearchAsync(new SourceRetrievalQuery
            {
                ExactCanonicalReference = hit.Segment.CanonicalReference,
                Languages = question.Languages,
                Collections = question.Collections,
                Categories = question.Categories,
                WorkKeys = question.WorkKeys,
                SourceKeys = question.SourceKeys,
                CandidateLimit = 20,
            }, cancellationToken).ConfigureAwait(false);
            var paired = referenceMatches
                .Where(candidate => !string.Equals(candidate.Segment.SegmentId, hit.Segment.SegmentId, StringComparison.Ordinal))
                .Where(candidate => IsTranslationPair(hit.Segment, candidate.Segment))
                .OrderByDescending(candidate => string.Equals(candidate.Segment.LanguageCode, "he", StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(candidate => candidate.Score)
                .FirstOrDefault();
            if (paired is not null)
            {
                TryAdd(paired.Segment, question.Question, items, seenSegments, documentCounts, ref characterCount);
            }

            if (items.Count >= options.MaximumEvidenceSegments || characterCount >= options.MaximumEvidenceCharacters)
            {
                break;
            }
            var context = await retriever.GetContextAsync(hit.Segment.DocumentId, hit.Segment.DocumentOrdinal, options.ContextRadius, cancellationToken).ConfigureAwait(false);
            foreach (var segment in context.OrderBy(segment => segment.DocumentOrdinal))
            {
                TryAdd(segment, question.Question, items, seenSegments, documentCounts, ref characterCount);
            }
        }

        return new EvidencePacket(items, characterCount);
    }

    private bool TryAdd(SourceSegment segment, string queryText, List<EvidenceItem> items, HashSet<string> seenSegments, Dictionary<string, int> documentCounts, ref int characterCount)
    {
        if (items.Count >= options.MaximumEvidenceSegments || seenSegments.Contains(segment.SegmentId))
        {
            return false;
        }
        documentCounts.TryGetValue(segment.DocumentId, out var currentDocumentCount);
        if (currentDocumentCount >= options.MaximumSegmentsPerDocument)
        {
            return false;
        }
        var remaining = options.MaximumEvidenceCharacters - characterCount;
        if (remaining < 200)
        {
            return false;
        }

        var presented = CreatePresentedText(segment, queryText, Math.Min(remaining, options.MaximumCharactersPerSegment));
        if (presented is null)
        {
            return false;
        }
        var item = new EvidenceItem($"E{items.Count + 1}", segment, presented.Value.Text, presented.Value.IsExcerpt, presented.Value.OriginalCharacterCount);
        items.Add(item);
        seenSegments.Add(segment.SegmentId);
        documentCounts[segment.DocumentId] = currentDocumentCount + 1;
        characterCount += item.PresentedText.Length;
        return true;
    }

    private static (string Text, bool IsExcerpt, int OriginalCharacterCount)? CreatePresentedText(SourceSegment segment, string queryText, int limit)
    {
        var text = segment.Text;
        if (segment.IsExcerpt)
        {
            if (segment.OriginalCharacterCount < text.Length || segment.ExcerptStart < 0 || segment.ExcerptStart + text.Length > segment.OriginalCharacterCount)
            {
                throw new InvalidDataException($"Provider excerpt '{segment.SegmentId}' has invalid character bounds.");
            }
            if (limit < 200)
            {
                return null;
            }

            var providerMarker = CreateExcerptMarker(segment.ExcerptStart, segment.ExcerptStart + text.Length, segment.OriginalCharacterCount);
            var availableText = limit - providerMarker.Length;
            if (availableText < 100)
            {
                return null;
            }
            if (text.Length <= availableText)
            {
                return (providerMarker + text, true, segment.OriginalCharacterCount);
            }

            var localStart = FindExcerptStart(text, queryText, availableText);
            var localEnd = Math.Min(text.Length, localStart + availableText);
            providerMarker = CreateExcerptMarker(segment.ExcerptStart + localStart, segment.ExcerptStart + localEnd, segment.OriginalCharacterCount);
            availableText = Math.Max(1, limit - providerMarker.Length);
            localStart = Math.Min(localStart, Math.Max(0, text.Length - availableText));
            localEnd = Math.Min(text.Length, localStart + availableText);
            providerMarker = CreateExcerptMarker(segment.ExcerptStart + localStart, segment.ExcerptStart + localEnd, segment.OriginalCharacterCount);
            return (providerMarker + text[localStart..localEnd], true, segment.OriginalCharacterCount);
        }
        if (text.Length <= limit)
        {
            return (text, false, text.Length);
        }
        if (limit < 200)
        {
            return null;
        }

        var bodyLength = Math.Max(100, limit - 100);
        var start = FindExcerptStart(text, queryText, bodyLength);
        var end = Math.Min(text.Length, start + bodyLength);
        var marker = $"[Explicit excerpt: characters {start + 1}-{end} of {text.Length}]\n";
        bodyLength = Math.Max(1, limit - marker.Length);
        start = Math.Min(start, Math.Max(0, text.Length - bodyLength));
        end = Math.Min(text.Length, start + bodyLength);
        marker = $"[Explicit excerpt: characters {start + 1}-{end} of {text.Length}]\n";
        if (marker.Length + end - start > limit)
        {
            end = Math.Max(start + 1, start + limit - marker.Length);
            marker = $"[Explicit excerpt: characters {start + 1}-{end} of {text.Length}]\n";
        }
        return (marker + text[start..end], true, text.Length);
    }

    private static string CreateExcerptMarker(int zeroBasedStart, int exclusiveEnd, int originalCharacterCount) => $"[Explicit excerpt: characters {zeroBasedStart + 1}-{exclusiveEnd} of {originalCharacterCount}]\n";

    private static int FindExcerptStart(string text, string queryText, int bodyLength)
    {
        foreach (var token in SearchTextNormalizer.Tokenize(queryText).OrderByDescending(token => token.Length))
        {
            var matchIndex = text.IndexOf(token, StringComparison.OrdinalIgnoreCase);
            if (matchIndex >= 0)
            {
                return Math.Clamp(matchIndex - bodyLength / 3, 0, Math.Max(0, text.Length - bodyLength));
            }
        }
        return 0;
    }

    private static bool IsTranslationPair(SourceSegment first, SourceSegment second)
    {
        var firstIsHebrew = string.Equals(first.LanguageCode, "he", StringComparison.OrdinalIgnoreCase);
        var secondIsHebrew = string.Equals(second.LanguageCode, "he", StringComparison.OrdinalIgnoreCase);
        return firstIsHebrew != secondIsHebrew && string.Equals(first.CanonicalReference, second.CanonicalReference, StringComparison.OrdinalIgnoreCase);
    }
}
