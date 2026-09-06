using System.Text;

namespace AskARabbiLIB.Grounding;

/// <summary>Renders a validated grounded answer as readable conversation text with numbered source references.</summary>
public sealed class GroundedAnswerTextRenderer
{
    /// <summary>Renders claims, source references, disagreements, an optional continuation, and practical guidance.</summary>
    /// <param name="answer">Validated grounded answer.</param>
    /// <returns>Plain conversation text suitable for persistence and presentation.</returns>
    public string Render(GroundedAnswer answer)
    {
        ArgumentNullException.ThrowIfNull(answer);
        var builder = new StringBuilder();
        var presentation = ConversationPresentationText.ForLanguage(answer.ResponseLanguage);
        foreach (var claim in answer.Claims)
        {
            AppendStatement(builder, claim.Text, claim.Citations);
        }
        if (answer.Disagreements.Count > 0)
        {
            AppendParagraph(builder, presentation.Perspective);
            foreach (var disagreement in answer.Disagreements)
            {
                AppendStatement(builder, disagreement.Text, disagreement.Citations);
            }
        }
        if (!string.IsNullOrWhiteSpace(answer.ClarifyingQuestion))
        {
            AppendParagraph(builder, $"{presentation.Continuation} {answer.ClarifyingQuestion}");
        }
        if (answer.HumanGuidanceRecommended)
        {
            AppendParagraph(builder, presentation.Guidance);
        }
        if (ConversationPersonalization.NormalizeLanguage(answer.QuotationLanguage) is { } requestedLanguage)
        {
            var preferredReferences = answer.Citations.Where(citation => ConversationPersonalization.NormalizeLanguage(citation.Language) == requestedLanguage)
                .Select(citation => citation.CanonicalReference).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var otherLanguages = answer.Citations.Where(citation => citation.Collection is not "Calendar calculations" and not "Technical background")
                .Where(citation => !preferredReferences.Contains(citation.CanonicalReference))
                .Select(citation => ConversationPersonalization.NormalizeLanguage(citation.Language) ?? citation.Language)
                .Where(language => !string.Equals(language, requestedLanguage, StringComparison.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (otherLanguages.Length > 0)
            {
                AppendParagraph(builder, string.Format(System.Globalization.CultureInfo.InvariantCulture, presentation.QuotationFallback, requestedLanguage, string.Join(", ", otherLanguages)));
            }
        }
        return builder.ToString().Trim();
    }

    private static void AppendStatement(StringBuilder builder, string text, IReadOnlyList<SourceCitation> citations)
    {
        AppendParagraph(builder, $"{text.Trim()} {FormatCitationNumbers(citations)}".TrimEnd());
    }

    private static string FormatCitationNumbers(IReadOnlyList<SourceCitation> citations) => string.Join(' ', citations.Select(citation => $"[{citation.Number}]"));

    private static void AppendParagraph(StringBuilder builder, string value)
    {
        if (builder.Length > 0)
        {
            builder.Append("\n\n");
        }
        builder.Append(value.Trim());
    }
}
