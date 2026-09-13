using System.Text.RegularExpressions;

namespace AskARabbiLIB.Grounding;

/// <summary>Inserts verified source wording into prose without asking the model to retype quotations.</summary>
internal static class GroundedInlineQuotationExpander
{
    private static readonly Regex Marker = new(@"\[\[quote:(?<id>E[1-9]\d*):(?<selector>@Q[1-9]\d*)\]\]", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    internal static bool TryExpand(GroundedAnswerDraft draft, EvidencePacket packet, out GroundedAnswerDraft expanded, out string? error)
    {
        var evidence = packet.Items.ToDictionary(item => item.EvidenceId, StringComparer.Ordinal);
        var claims = new List<GroundedClaimDraft>();
        var disagreements = new List<GroundedSourcedStatementDraft>();
        expanded = draft;
        foreach (var claim in draft.Claims)
        {
            if (!TryExpandText(claim.Text, claim.EvidenceIds, evidence, out var text, out error))
            {
                return false;
            }
            claims.Add(claim with { Text = text });
        }
        foreach (var disagreement in draft.Disagreements)
        {
            if (!TryExpandText(disagreement.Text, disagreement.EvidenceIds, evidence, out var text, out error))
            {
                return false;
            }
            disagreements.Add(disagreement with { Text = text });
        }
        expanded = draft with { Claims = claims, Disagreements = disagreements };
        error = null;
        return true;
    }

    private static bool TryExpandText(string text, IReadOnlyList<string> citedIds, IReadOnlyDictionary<string, EvidenceItem> evidence, out string expanded, out string? error)
    {
        var valid = true;
        // Inspect unresolved syntax before expansion so marker-like source content
        // remains source data, not an instruction for recursive substitution.
        var hasMalformedMarker = Marker.Replace(text, string.Empty).Contains("[[quote:", StringComparison.OrdinalIgnoreCase);
        expanded = Marker.Replace(text, match =>
        {
            var id = match.Groups["id"].Value;
            if (!citedIds.Contains(id, StringComparer.Ordinal) || !evidence.TryGetValue(id, out var item) || !GroundedQuotationResolver.TryResolve(item, match.Groups["selector"].Value, out var quotation))
            {
                valid = false;
                return match.Value;
            }
            return $"“{quotation}”";
        });
        error = valid && !hasMalformedMarker ? null : "An inline quotation marker is invalid. Use [[quote:E1:@Q2]] only with an actual cited evidence ID and one of its supplied quotationChoices; never invent a selector.";
        return error is null;
    }
}
