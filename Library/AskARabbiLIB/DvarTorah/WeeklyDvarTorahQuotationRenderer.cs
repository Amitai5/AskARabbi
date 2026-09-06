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
        foreach (var id in selectedIds)
        {
            if (!evidenceById.TryGetValue(id, out var item) || CreateInlineQuotation(item) is not { } quotation)
            {
                continue;
            }

            var slot = GetQuotationSlot(id);
            var index = body.IndexOf(slot, StringComparison.Ordinal);
            if (index < 0 || body.IndexOf(slot, index + slot.Length, StringComparison.Ordinal) >= 0)
            {
                // Missing or repeated slots must be repaired, never moved to an unrelated paragraph.
                continue;
            }
            body = body.Replace(slot, quotation, StringComparison.Ordinal);
        }

        return body != draft.Body && body.Length <= maximumBodyCharacters ? draft with { Body = body } : draft;
    }

    internal static string GetQuotationSlot(string evidenceId) => "{{quote:" + evidenceId + "}}";

    internal static string? CreateInlineQuotation(WeeklyDvarTorahEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        if (evidence.Kind != WeeklyDvarTorahSourceKind.Torah || string.IsNullOrWhiteSpace(evidence.PresentedText) || string.IsNullOrWhiteSpace(evidence.CanonicalReference))
        {
            return null;
        }

        var quotation = BoundQuotation(CollapseWhitespace(evidence.PresentedText));
        // The source button carries the canonical reference; only the quotation belongs in the speech.
        return $"“{quotation}” [{evidence.EvidenceId}]";
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
