using System.Text.RegularExpressions;
using AskARabbiLIB.Conversations;
using AskARabbiLIB.Grounding;

namespace AskARabbi.Api.Conversations;

internal static partial class ConversationSourceMaterializer
{
    internal static IReadOnlyList<ConversationSourceCitation> Materialize(GroundedAnswer answer, EvidencePacket evidence)
    {
        ArgumentNullException.ThrowIfNull(answer);
        ArgumentNullException.ThrowIfNull(evidence);
        var evidenceById = evidence.Items.ToDictionary(item => item.EvidenceId, StringComparer.Ordinal);
        var quotationsByEvidenceId = CollectQuotations(answer);
        var sources = new List<ConversationSourceCitation>(answer.Citations.Count);

        foreach (var citation in answer.Citations.OrderBy(value => value.Number))
        {
            if (!evidenceById.TryGetValue(citation.EvidenceId, out var item))
            {
                throw new InvalidOperationException($"Validated citation {citation.Number} does not resolve to its trusted evidence item.");
            }

            sources.Add(new ConversationSourceCitation
            {
                Number = citation.Number,
                Title = citation.Title,
                HebrewTitle = citation.HebrewTitle,
                CanonicalReference = citation.CanonicalReference,
                Edition = citation.Edition,
                Language = citation.Language,
                Collection = citation.Collection,
                License = citation.License,
                SourceUrl = string.Equals(citation.Collection, "Calendar calculations", StringComparison.Ordinal) ? citation.SourceUrl : CreateCanonicalSourceUrl(citation.CanonicalReference),
                AttributionUrl = citation.SourceUrl,
                Quotations = quotationsByEvidenceId.GetValueOrDefault(citation.EvidenceId, []),
                Context = item.PresentedText,
                IsExcerpt = item.IsExcerpt,
            });
        }

        return sources;
    }

    internal static string CreateCanonicalSourceUrl(string canonicalReference)
    {
        if (string.IsNullOrWhiteSpace(canonicalReference))
        {
            throw new ArgumentException("A canonical reference is required.", nameof(canonicalReference));
        }

        var referencePath = WhitespacePattern().Replace(canonicalReference.Trim(), "_").Replace(':', '.');
        return new UriBuilder(Uri.UriSchemeHttps, "www.sefaria.org") { Path = referencePath }.Uri.AbsoluteUri;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> CollectQuotations(GroundedAnswer answer)
    {
        var quotations = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var claim in answer.Claims)
        {
            AddQuotations(quotations, claim.Quotations);
            if (!string.IsNullOrWhiteSpace(claim.DirectQuotation) && claim.QuotationSource is not null)
            {
                AddQuotation(quotations, claim.QuotationSource.EvidenceId, claim.DirectQuotation);
            }
        }
        foreach (var disagreement in answer.Disagreements)
        {
            AddQuotations(quotations, disagreement.Quotations);
        }

        return quotations.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<string>)pair.Value.ToArray(), StringComparer.Ordinal);
    }

    private static void AddQuotations(IDictionary<string, List<string>> quotations, IEnumerable<GroundedQuotation> values)
    {
        foreach (var quotation in values)
        {
            AddQuotation(quotations, quotation.Source.EvidenceId, quotation.Text);
        }
    }

    private static void AddQuotation(IDictionary<string, List<string>> quotations, string evidenceId, string value)
    {
        if (!quotations.TryGetValue(evidenceId, out var values))
        {
            values = [];
            quotations.Add(evidenceId, values);
        }
        if (!values.Contains(value, StringComparer.Ordinal))
        {
            values.Add(value);
        }
    }

    [GeneratedRegex("\\s+")]
    private static partial Regex WhitespacePattern();
}
