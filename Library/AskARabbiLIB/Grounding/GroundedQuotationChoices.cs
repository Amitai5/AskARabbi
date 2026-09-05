using System.Text.RegularExpressions;

namespace AskARabbiLIB.Grounding;

/// <summary>Offers exact source substrings that a model can select without recopying or merging quotations.</summary>
internal static class GroundedQuotationChoices
{
    internal static IReadOnlyList<Choice> Create(EvidenceItem evidence)
    {
        var choices = new List<Choice>();
        foreach (Match match in Regex.Matches(evidence.Source.Text, @"[^\n.!?]+(?:[.!?]+|$)", RegexOptions.Multiline))
        {
            var text = match.Value.Trim();
            if (text.Length is < 12 or > 1_000 || text.StartsWith('[') || !evidence.PresentedText.Contains(text, StringComparison.Ordinal))
            {
                continue;
            }
            choices.Add(new Choice($"@Q{choices.Count + 1}", text));
        }
        return choices;
    }

    internal sealed record Choice(string Selector, string Text);
}
