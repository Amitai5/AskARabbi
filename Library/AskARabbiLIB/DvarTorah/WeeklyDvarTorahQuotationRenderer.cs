namespace AskARabbiLIB.DvarTorah;

internal static class WeeklyDvarTorahQuotationRenderer
{
    internal const int RequiredQuotationCount = 3;
    internal const int ReservedBodyCharacters = 2_700;
    private const int MaximumQuotationCharacters = 600;

    internal static WeeklyDvarTorahArticleDraft AddTrustedQuotations(WeeklyDvarTorahArticleDraft draft, IReadOnlyList<WeeklyDvarTorahEvidence> evidence, int maximumBodyCharacters)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(evidence);
        if (string.IsNullOrWhiteSpace(draft.Body))
        {
            return draft;
        }

        var evidenceById = evidence.ToDictionary(item => item.EvidenceId, StringComparer.Ordinal);
        var selectedIds = (draft.FeaturedTorahEvidenceIds ?? [])
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var body = draft.Body.Trim().Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var paragraphs = body.Split("\n\n", StringSplitOptions.None);
        var additions = new Dictionary<int, List<string>>();
        foreach (var id in selectedIds)
        {
            if (!evidenceById.TryGetValue(id, out var item) || CreateQuotationLine(item) is not { } quotationLine || body.Contains(quotationLine, StringComparison.Ordinal))
            {
                continue;
            }

            var paragraphIndex = Array.FindIndex(paragraphs, paragraph => paragraph.Contains($"[{id}]", StringComparison.Ordinal));
            if (paragraphIndex < 0)
            {
                continue;
            }
            if (!additions.TryGetValue(paragraphIndex, out var lines))
            {
                lines = [];
                additions[paragraphIndex] = lines;
            }
            lines.Add(quotationLine);
        }

        if (additions.Count == 0)
        {
            return draft;
        }

        var renderedParagraphs = new List<string>(paragraphs.Length + selectedIds.Length);
        for (var index = 0; index < paragraphs.Length; index++)
        {
            renderedParagraphs.Add(paragraphs[index]);
            if (additions.TryGetValue(index, out var quotationLines))
            {
                renderedParagraphs.AddRange(quotationLines);
            }
        }

        var renderedBody = string.Join("\n\n", renderedParagraphs);
        return renderedBody.Length <= maximumBodyCharacters ? draft with { Body = renderedBody } : draft;
    }

    internal static string? CreateQuotationLine(WeeklyDvarTorahEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        if (evidence.Kind != WeeklyDvarTorahSourceKind.Torah || string.IsNullOrWhiteSpace(evidence.PresentedText) || string.IsNullOrWhiteSpace(evidence.CanonicalReference))
        {
            return null;
        }

        var quotation = BoundQuotation(CollapseWhitespace(evidence.PresentedText));
        var canonicalReference = CollapseWhitespace(evidence.CanonicalReference);
        return $"Torah text — {canonicalReference}: “{quotation}” [{evidence.EvidenceId}]";
    }

    internal static int GetMaximumGeneratedBodyCharacters(WeeklyDvarTorahContentOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return Math.Max(options.MinimumBodyCharacters, options.MaximumBodyCharacters - ReservedBodyCharacters);
    }

    private static string CollapseWhitespace(string value) => string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string BoundQuotation(string value)
    {
        if (value.Length <= MaximumQuotationCharacters)
        {
            return value;
        }

        var boundary = value.LastIndexOf(' ', MaximumQuotationCharacters - 1);
        var length = boundary >= MaximumQuotationCharacters / 2 ? boundary : MaximumQuotationCharacters;
        return $"{value[..length].TrimEnd()}…";
    }
}
