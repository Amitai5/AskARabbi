using AskARabbiLIB.Grounding;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class GroundedInlineQuotationExpanderTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void TryExpand_RealCitedSelector_InsertsExactWordingInClaimsAndDisagreements(bool disagreement)
    {
        var packet = Packet();
        var choice = GroundedQuotationChoices.Create(packet.Items[0])[0];
        var text = $"The passage says [[quote:E1:{choice.Selector}]] This is the explanation.";
        var draft = Draft(text);
        if (disagreement)
        {
            draft = draft with { Claims = [], Disagreements = [new GroundedSourcedStatementDraft { Text = text, EvidenceIds = ["E1"], Quotations = draft.Claims[0].Quotations }] };
        }

        var valid = GroundedInlineQuotationExpander.TryExpand(draft, packet, out var expanded, out var error);

        Assert.IsTrue(valid, error);
        var actual = disagreement ? expanded.Disagreements[0].Text : expanded.Claims[0].Text;
        Assert.AreEqual($"The passage says “{choice.Text}” This is the explanation.", actual);
        Assert.IsFalse(actual.Contains("[[quote:", StringComparison.Ordinal));
        Assert.AreEqual(text, disagreement ? draft.Disagreements[0].Text : draft.Claims[0].Text);
    }

    [TestMethod]
    [DataRow("An invalid [[quote:E99:@Q1]] marker.")]
    [DataRow("An invalid [[quote:E1:@Q999]] marker.")]
    [DataRow("An invalid [[quote:E1:Q1]] marker.")]
    [DataRow("An invalid [[quote:E1:@Q1 marker.")]
    public void TryExpand_InvalidSelectorOrSyntax_FailsClosed(string text)
    {
        Assert.IsFalse(GroundedInlineQuotationExpander.TryExpand(Draft(text), Packet(), out _, out var error));
        Assert.IsNotNull(error);
    }

    [TestMethod]
    public void TryExpand_UncitedSource_RejectsTheMarker()
    {
        var draft = Draft("The passage says [[quote:E1:@Q1]].");
        draft = draft with { Claims = [draft.Claims[0] with { EvidenceIds = ["E2"] }] };

        Assert.IsFalse(GroundedInlineQuotationExpander.TryExpand(draft, Packet(), out _, out _));
    }

    [TestMethod]
    public void TryExpand_InvalidDisagreementMarker_FailsClosedWithoutReturningPartialExpansion()
    {
        var draft = Draft("The passage says [[quote:E1:@Q1]].");
        draft = draft with { Disagreements = [new GroundedSourcedStatementDraft { Text = "[[quote:E1:@Q999]]", EvidenceIds = ["E1"], Quotations = draft.Claims[0].Quotations }] };

        Assert.IsFalse(GroundedInlineQuotationExpander.TryExpand(draft, Packet(), out var expanded, out _));
        Assert.AreSame(draft, expanded);
    }

    [TestMethod]
    public void TryExpand_NoMarkers_LeavesProseUnchanged()
    {
        var draft = Draft("A plain explanation with no placeholder.");

        Assert.IsTrue(GroundedInlineQuotationExpander.TryExpand(draft, Packet(), out var expanded, out _));
        Assert.AreEqual(draft.Claims[0].Text, expanded.Claims[0].Text);
    }

    [TestMethod]
    public void TryExpand_ForeignInlineQuotation_DoesNotBypassQuotationLanguageValidation()
    {
        var english = SourceResearchTestData.Passage("Hear, O Israel: the Lord our God is one.");
        var hebrew = english with { SegmentId = "hebrew", Language = "Hebrew", LanguageCode = "he", Text = "שמע ישראל יהוה אלהינו יהוה אחד" };
        var packet = new EvidencePacket([new EvidenceItem("E1", english, english.Text, false, english.Text.Length), new EvidenceItem("E2", hebrew, hebrew.Text, false, hebrew.Text.Length)], english.Text.Length + hebrew.Text.Length);
        var draft = Draft("The text says [[quote:E2:@Q1]].");
        draft = draft with { Claims = [draft.Claims[0] with { EvidenceIds = ["E2"], Quotations = [new GroundedQuotationDraft { EvidenceId = "E2", Text = "@Q1", Role = "Wording" }] }] };

        Assert.IsTrue(GroundedInlineQuotationExpander.TryExpand(draft, packet, out var expanded, out _));
        Assert.IsFalse(new ConversationPersonalization("English", "English", null).TryValidateQuotationLanguages(expanded, packet, out _));
    }

    private static EvidencePacket Packet()
    {
        var source = SourceResearchTestData.Passage();
        return new EvidencePacket([new EvidenceItem("E1", source, source.Text, false, source.Text.Length)], source.Text.Length);
    }

    private static GroundedAnswerDraft Draft(string text) => new()
    {
        Claims = [new GroundedClaimDraft { Text = text, EvidenceIds = ["E1"], Quotations = [new GroundedQuotationDraft { EvidenceId = "E1", Text = "@Q1", Role = "The exact wording." }] }],
        Disagreements = [], Limitations = [], HumanGuidanceRecommended = false,
    };
}
