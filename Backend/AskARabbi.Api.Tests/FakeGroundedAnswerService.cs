using AskARabbiLIB.Grounding;
using AskARabbiLIB.Models;
using AskARabbiLIB.Retrieval;

namespace AskARabbi.Api.Tests;

internal sealed class FakeGroundedAnswerService : IGroundedAnswerService
{
    internal int CallCount { get; private set; }

    internal GroundedQuestion? LastQuestion { get; private set; }

    internal GroundedAnswerResult? NextResult { get; set; }

    public Task<GroundedAnswerResult> AnswerAsync(GroundedQuestion question, IReadOnlyList<GroundedConversationTurn> recentConversation, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CallCount++;
        LastQuestion = question;
        if (NextResult is { } nextResult)
        {
            NextResult = null;
            return Task.FromResult(nextResult);
        }
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
            SuggestedConversationTitle = question.ShouldGenerateConversationTitle ? "Jewish Customs and Practice" : null,
            InterpretiveNotice = "AskRabbi offers source-based Jewish learning, not personal halakhic rulings.",
        };
        return Task.FromResult(new GroundedAnswerResult
        {
            Status = GroundedAnswerStatus.Success,
            Answer = answer,
            Evidence = new EvidencePacket([new EvidenceItem("E1", CreateSourceSegment(), "The surrounding tested source context includes the exact statement: The tested source text. It also includes the next line of context.", false, 128)], 128),
            Trace = new GroundedAnswerTrace(TimeSpan.Zero, TimeSpan.Zero, 1, 1, 23, null, GroundedValidationStatus.Passed, false, "test-response", "test-model"),
        });
    }

    private static SourceSegment CreateSourceSegment() => new()
    {
        SegmentId = "sefaria:test:segment:00000001",
        DocumentId = "sefaria:test",
        CanonicalReference = "Test 1:1",
        DocumentOrdinal = 1,
        Text = "The surrounding tested source context includes the exact statement: The tested source text. It also includes the next line of context.",
        Title = "Test source",
        HebrewTitle = "מקור",
        Language = "English",
        LanguageCode = "en",
        Collection = "Torah",
        Categories = ["Torah"],
        Version = "Test edition",
        License = "CC-BY",
        LicenseCategory = SourceLicenseCategory.CcBy,
        SourceUrl = "https://example.test/source",
        FilePath = "Data/NormalizedData/Sefaria/Test.md",
    };
}
