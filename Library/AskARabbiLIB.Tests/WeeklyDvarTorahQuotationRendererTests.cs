using AskARabbiLIB.DvarTorah;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class WeeklyDvarTorahQuotationRendererTests
{
    private static readonly DateTimeOffset RetrievedAtUtc = new(2026, 9, 2, 20, 0, 0, TimeSpan.Zero);

    [TestMethod]
    [TestCategory("Unit")]
    public void AddTrustedQuotations_ValidSelections_InsertsExactQuotesAfterCitingParagraph()
    {
        var first = CreateEvidence("T1", "  Choose   life.  ", " Deuteronomy   30:19 ");
        var second = CreateEvidence("T2", "The word is very near to you.", "Deuteronomy 30:14");
        var news = CreateEvidence("N1", "News summary.", null, WeeklyDvarTorahSourceKind.News);
        var draft = CreateDraft("Opening [T1] and [T2].\r\n\r\nClosing paragraph.", ["T1", "", "T1", "UNKNOWN", "N1", "T2"]);

        var result = WeeklyDvarTorahQuotationRenderer.AddTrustedQuotations(draft, [first, second, news], 2_000);

        Assert.AreNotSame(draft, result);
        Assert.AreEqual("Opening [T1] and [T2].\n\nTorah text — Deuteronomy 30:19: “Choose life.” [T1]\n\nTorah text — Deuteronomy 30:14: “The word is very near to you.” [T2]\n\nClosing paragraph.", result.Body);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void AddTrustedQuotations_ExistingQuoteOrMissingMarker_ReturnsOriginalDraft()
    {
        var evidence = CreateEvidence("T1", "Choose life.", "Deuteronomy 30:19");
        var quotation = WeeklyDvarTorahQuotationRenderer.CreateQuotationLine(evidence);
        Assert.IsNotNull(quotation);
        var existingQuote = CreateDraft($"Teaching [T1].\n\n{quotation}", ["T1"]);
        var missingMarker = CreateDraft("Teaching without a marker.", ["T1"]);

        var existingResult = WeeklyDvarTorahQuotationRenderer.AddTrustedQuotations(existingQuote, [evidence], 2_000);
        var missingResult = WeeklyDvarTorahQuotationRenderer.AddTrustedQuotations(missingMarker, [evidence], 2_000);

        Assert.AreSame(existingQuote, existingResult);
        Assert.AreSame(missingMarker, missingResult);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void AddTrustedQuotations_BlankBodyOrInvalidEvidence_ReturnsOriginalDraft()
    {
        var blankBody = CreateDraft(" ", ["T1"]);
        var missingSelection = CreateDraft("Teaching without a selection.", null!);
        var invalidEvidence = CreateEvidence("T1", "News summary.", null, WeeklyDvarTorahSourceKind.News);
        var invalidDraft = CreateDraft("Teaching [T1].", ["T1"]);

        var blankResult = WeeklyDvarTorahQuotationRenderer.AddTrustedQuotations(blankBody, [], 2_000);
        var missingSelectionResult = WeeklyDvarTorahQuotationRenderer.AddTrustedQuotations(missingSelection, [], 2_000);
        var invalidResult = WeeklyDvarTorahQuotationRenderer.AddTrustedQuotations(invalidDraft, [invalidEvidence], 2_000);

        Assert.AreSame(blankBody, blankResult);
        Assert.AreSame(missingSelection, missingSelectionResult);
        Assert.AreSame(invalidDraft, invalidResult);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void AddTrustedQuotations_ResultExceedsMaximumBodyCharacters_ReturnsOriginalDraft()
    {
        var evidence = CreateEvidence("T1", "Choose life.", "Deuteronomy 30:19");
        var draft = CreateDraft("Teaching [T1].", ["T1"]);

        var result = WeeklyDvarTorahQuotationRenderer.AddTrustedQuotations(draft, [evidence], draft.Body.Length);

        Assert.AreSame(draft, result);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void AddTrustedQuotations_NullInputs_Throw()
    {
        var draft = CreateDraft("Teaching [T1].", ["T1"]);

        Assert.ThrowsExactly<ArgumentNullException>(() => WeeklyDvarTorahQuotationRenderer.AddTrustedQuotations(null!, [], 2_000));
        Assert.ThrowsExactly<ArgumentNullException>(() => WeeklyDvarTorahQuotationRenderer.AddTrustedQuotations(draft, null!, 2_000));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void CreateQuotationLine_ValidEvidence_BoundsAndFormatsTrustedText()
    {
        var spacedText = string.Join(' ', Enumerable.Repeat("covenantal", 80));
        var evidence = CreateEvidence("T1", spacedText, " Deuteronomy   30:19 ");

        var result = WeeklyDvarTorahQuotationRenderer.CreateQuotationLine(evidence);

        Assert.IsNotNull(result);
        StringAssert.StartsWith(result, "Torah text — Deuteronomy 30:19: “");
        StringAssert.EndsWith(result, "…” [T1]");
        Assert.IsTrue(result.Length < spacedText.Length);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void CreateQuotationLine_LongTextWithoutWordBoundary_UsesHardLimit()
    {
        var evidence = CreateEvidence("T1", new string('a', 601), "Deuteronomy 30:19");

        var result = WeeklyDvarTorahQuotationRenderer.CreateQuotationLine(evidence);

        Assert.IsNotNull(result);
        StringAssert.Contains(result, $"“{new string('a', 600)}…”");
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void CreateQuotationLine_InvalidEvidence_ReturnsNull()
    {
        WeeklyDvarTorahEvidence[] invalidEvidence =
        [
            CreateEvidence("N1", "News summary.", "Reference", WeeklyDvarTorahSourceKind.News),
            CreateEvidence("T1", " ", "Deuteronomy 30:19"),
            CreateEvidence("T1", "Choose life.", " "),
        ];

        foreach (var evidence in invalidEvidence)
        {
            Assert.IsNull(WeeklyDvarTorahQuotationRenderer.CreateQuotationLine(evidence));
        }
        Assert.ThrowsExactly<ArgumentNullException>(() => WeeklyDvarTorahQuotationRenderer.CreateQuotationLine(null!));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void GetMaximumGeneratedBodyCharacters_UsesReservedSpaceWithoutDroppingBelowMinimum()
    {
        var normal = new WeeklyDvarTorahContentOptions { MinimumBodyCharacters = 2_500, MaximumBodyCharacters = 15_000 };
        var narrow = new WeeklyDvarTorahContentOptions { MinimumBodyCharacters = 4_000, MaximumBodyCharacters = 5_000 };

        var normalResult = WeeklyDvarTorahQuotationRenderer.GetMaximumGeneratedBodyCharacters(normal);
        var narrowResult = WeeklyDvarTorahQuotationRenderer.GetMaximumGeneratedBodyCharacters(narrow);

        Assert.AreEqual(12_300, normalResult);
        Assert.AreEqual(4_000, narrowResult);
        Assert.ThrowsExactly<ArgumentNullException>(() => WeeklyDvarTorahQuotationRenderer.GetMaximumGeneratedBodyCharacters(null!));
    }

    private static WeeklyDvarTorahArticleDraft CreateDraft(string body, IReadOnlyList<string> featuredTorahEvidenceIds) => new()
    {
        Title = "Choosing Life",
        Body = body,
        FeaturedTorahEvidenceIds = featuredTorahEvidenceIds,
        CentralTeaching = "Choose life through faithful and compassionate action in the world.",
        Tags = ["nitzavim", "life", "responsibility", "community", "action"],
        PracticalActions = ["Study Torah.", "Listen carefully.", "Help a neighbor."],
        TorahTeachings = [],
        CurrentEventFacts = [],
        Connections = [],
    };

    private static WeeklyDvarTorahEvidence CreateEvidence(string id, string presentedText, string? canonicalReference, WeeklyDvarTorahSourceKind kind = WeeklyDvarTorahSourceKind.Torah) => new(id, kind, "Deuteronomy", "Test publisher", "https://example.test/source", presentedText, RetrievedAtUtc, canonicalReference, null, "Public Domain");
}
