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
        foreach (var claim in answer.Claims)
        {
            AppendStatement(builder, claim.Text, claim.Citations);
        }
        if (answer.Disagreements.Count > 0)
        {
            AppendParagraph(builder, "Another perspective:");
            foreach (var disagreement in answer.Disagreements)
            {
                AppendStatement(builder, disagreement.Text, disagreement.Citations);
            }
        }
        if (!string.IsNullOrWhiteSpace(answer.ClarifyingQuestion))
        {
            AppendParagraph(builder, $"If you'd like to keep exploring: {answer.ClarifyingQuestion}");
        }
        if (answer.HumanGuidanceRecommended)
        {
            AppendParagraph(builder, "Because the practical answer may depend on your circumstances, talk it through with a qualified rabbi who knows your situation.");
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
