using System.Text;

namespace AskARabbiLIB.Grounding;

/// <summary>Renders a validated grounded answer as readable conversation text with inline citations.</summary>
public sealed class GroundedAnswerTextRenderer
{
    /// <summary>Renders claims, exact quotations, disagreements, limits, guidance, and the application-owned notice.</summary>
    /// <param name="answer">Validated grounded answer.</param>
    /// <returns>Plain conversation text suitable for persistence and presentation.</returns>
    public string Render(GroundedAnswer answer)
    {
        ArgumentNullException.ThrowIfNull(answer);
        var builder = new StringBuilder();
        var renderedQuotations = new HashSet<string>(StringComparer.Ordinal);
        foreach (var claim in answer.Claims)
        {
            AppendStatement(builder, claim.Text, claim.Citations, claim.Quotations, renderedQuotations);
        }
        if (answer.Disagreements.Count > 0)
        {
            AppendParagraph(builder, "Another perspective:");
            foreach (var disagreement in answer.Disagreements)
            {
                AppendStatement(builder, disagreement.Text, disagreement.Citations, disagreement.Quotations, renderedQuotations);
            }
        }
        if (answer.Limitations.Count > 0)
        {
            AppendParagraph(builder, $"What these sources do not fully answer: {string.Join(' ', answer.Limitations)}");
        }
        if (!string.IsNullOrWhiteSpace(answer.ClarifyingQuestion))
        {
            AppendParagraph(builder, $"A useful next question: {answer.ClarifyingQuestion} Ask me that next if you want to keep digging.");
        }
        if (answer.HumanGuidanceRecommended)
        {
            AppendParagraph(builder, "Because the practical answer may depend on your circumstances, talk it through with a qualified rabbi who knows your situation.");
        }
        AppendParagraph(builder, answer.InterpretiveNotice.Trim());
        return builder.ToString().Trim();
    }

    private static void AppendStatement(StringBuilder builder, string text, IReadOnlyList<SourceCitation> citations, IReadOnlyList<GroundedQuotation> quotations, ISet<string> renderedQuotations)
    {
        AppendParagraph(builder, $"{text.Trim()} {FormatCitationNumbers(citations)}".TrimEnd());
        foreach (var quotation in quotations)
        {
            var key = $"{quotation.Source.SegmentId}\n{quotation.Text}";
            if (!renderedQuotations.Add(key))
            {
                continue;
            }
            AppendParagraph(builder, $"“{quotation.Text}”\n— {quotation.Source.CanonicalReference} [{quotation.Source.Number}] ({quotation.Source.SourceUrl})");
        }
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
