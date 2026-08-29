using AskARabbiLIB.Grounding;
using AskARabbiLIB.Models;

namespace AskARabbi.Api.Tests;

internal sealed class FakeGroundedAnswerService : IGroundedAnswerService
{
    internal int CallCount { get; private set; }

    internal GroundedQuestion? LastQuestion { get; private set; }

    public Task<GroundedAnswerResult> AnswerAsync(GroundedQuestion question, IReadOnlyList<GroundedConversationTurn> recentConversation, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CallCount++;
        LastQuestion = question;
        var citation = new SourceCitation(1, "E1", "sefaria:test:segment:00000001", "Test source", "מקור", "Test 1:1", "Test edition", "English", "en", "Torah", ["Torah"], "CC-BY", SourceLicenseCategory.CcBy, "https://example.test/source", "Data/NormalizedData/Sefaria/Test.md", false);
        var quotation = new GroundedQuotation("The tested source text.", "Direct support for the test answer", citation);
        var answer = new GroundedAnswer(
            [new GroundedClaim("The validated test answer addresses the question.", [citation], quotation.Text, citation) { Attribution = "The test source", Quotations = [quotation] }],
            [],
            [],
            null,
            false,
            [citation])
        {
            InterpretiveNotice = "AskRabbi offers source-based Jewish learning, not personal halakhic rulings.",
        };
        return Task.FromResult(new GroundedAnswerResult
        {
            Status = GroundedAnswerStatus.Success,
            Answer = answer,
            Evidence = new EvidencePacket([], 0),
            Trace = new GroundedAnswerTrace(TimeSpan.Zero, TimeSpan.Zero, 1, 1, 23, null, GroundedValidationStatus.Passed, false, "test-response", "test-model"),
        });
    }
}
