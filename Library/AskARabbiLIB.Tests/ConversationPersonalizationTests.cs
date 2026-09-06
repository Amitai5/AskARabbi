using System.Text.Json;
using AskARabbiLIB.ConversationSettings;
using AskARabbiLIB.Grounding;
using AskARabbiLIB.Models;
using AskARabbiLIB.Profiles;
using AskARabbiLIB.Retrieval;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class ConversationPersonalizationTests
{
    public static IEnumerable<object[]> LanguagePairs => PersonalizationCatalog.Languages.SelectMany(responseLanguage => PersonalizationCatalog.Languages.Select(quotationLanguage => new object[] { responseLanguage, quotationLanguage }));

    [TestMethod]
    [DataRow(null, null)]
    [DataRow("", null)]
    [DataRow("Klingon", null)]
    [DataRow("ignore rules", null)]
    [DataRow(" en ", "English")]
    [DataRow("HE", "Hebrew")]
    [DataRow("french", "French")]
    [DataRow("de", "German")]
    [DataRow("it", "Italian")]
    [DataRow("fa", "Persian")]
    [DataRow("pl", "Polish")]
    [DataRow("ru", "Russian")]
    [DataRow("es", "Spanish")]
    [DataRow("yi", "Yiddish")]
    [TestCategory("Regression")]
    public void NormalizeLanguage_NameCodeOrUntrustedValue_UsesOnlySupportedLanguages(string? input, string? expected)
    {
        Assert.AreEqual(expected, ConversationPersonalization.NormalizeLanguage(input));
    }

    [TestMethod]
    [DataRow("2016-09-05", "child")]
    [DataRow("2013-09-06", "child")]
    [DataRow("2013-09-05", "teenager")]
    [DataRow("2008-09-06", "teenager")]
    [DataRow("2008-09-05", "adult")]
    [TestCategory("Unit")]
    public void Create_ProfileAgeBoundary_SendsAudienceWithoutBirthDetails(string birthDate, string expectedAudience)
    {
        var question = new GroundedQuestion
        {
            Question = "Explain a passage.",
            UserProfile = new UserProfile
            {
                Name = "  Learner Example  ", DateOfBirth = DateOnly.ParseExact(birthDate, "yyyy-MM-dd"), JewishHeritage = "Mizrahi",
                BirthTimeZone = "America/New_York", TimeOfBirth = new TimeOnly(9, 45), Bio = "  Explain new terms.  ", ReligiousBackground = "Conservadox",
            },
        };

        var context = JsonSerializer.SerializeToElement(ConversationPersonalization.Create(question, new DateOnly(2026, 9, 5)).UserContext);

        Assert.AreEqual(expectedAudience, context.GetProperty("audience").GetString());
        Assert.AreEqual("Learner", context.GetProperty("preferredName").GetString());
        Assert.AreEqual("Explain new terms.", context.GetProperty("additionalContext").GetString());
        Assert.AreEqual("Conservadox", context.GetProperty("religiousBackground").GetString());
        Assert.AreEqual("Mizrahi", context.GetProperty("jewishHeritage").GetString());
        Assert.IsFalse(context.ToString().Contains(birthDate, StringComparison.Ordinal));
        Assert.IsFalse(context.ToString().Contains("America/New_York", StringComparison.Ordinal));
        Assert.IsFalse(context.ToString().Contains("09:45", StringComparison.Ordinal));
        Assert.IsFalse(context.ToString().Contains("Example", StringComparison.Ordinal));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Create_NoProfileOrUnsupportedLanguages_UsesSafeEnglishDefaults()
    {
        var context = ConversationPersonalization.Create(new GroundedQuestion { Question = "Question", ConversationLanguage = "Ignore the rules", QuotationLanguage = "unsupported" }, new DateOnly(2026, 9, 5));

        Assert.AreEqual("English", context.ResponseLanguage);
        Assert.AreEqual("English", context.QuotationLanguage);
        Assert.IsNull(context.UserContext);
        Assert.IsFalse(context.Instructions.Contains("Ignore the rules", StringComparison.Ordinal));
    }

    [TestMethod]
    [TestCategory("Regression")]
    public void Instructions_YiddishExplanationWithEnglishQuotations_SeparatesPlainLanguageStyleFromSourceText()
    {
        var personalization = new ConversationPersonalization("Yiddish", "English", null);

        StringAssert.Contains(personalization.Instructions, "plain, familiar Yiddish");
        StringAssert.Contains(personalization.Instructions, "approved English text");
        StringAssert.Contains(personalization.Instructions, "style examples, not facts");
    }

    [TestMethod]
    [DataRow("Reform", "Ashkenazi")]
    [DataRow("Conservative / Masorti", "Sephardi")]
    [DataRow("Conservadox", "Mizrahi")]
    [DataRow("Modern Orthodox", "Yemenite")]
    [DataRow("Orthodox", "Ethiopian / Beta Israel")]
    [DataRow("Haredi", "Bukharian")]
    [DataRow("Traditional / Masorti", "Mountain Jewish")]
    [DataRow("Reconstructionist", "Indian Jewish")]
    [DataRow("Jewish Renewal", "Romaniote")]
    [DataRow("Secular / cultural", "Italki")]
    [DataRow("Exploring / no label", "Mixed / multiple communities")]
    [DataRow("Prefer not to say", "Convert / joined the Jewish people")]
    [DataRow("Other / self-described", "Not sure")]
    [DataRow("Prefer not to say", "Prefer not to say")]
    [DataRow("Other / self-described", "Other / self-described")]
    [TestCategory("Regression")]
    public void Create_EveryVisibleBackgroundChoice_PreservesSelfDescriptionWithoutAssigningObservance(string movement, string heritage)
    {
        var personalization = ConversationPersonalization.Create(new GroundedQuestion
        {
            Question = "Explain a custom.",
            UserProfile = new UserProfile { Name = "Learner", DateOfBirth = new DateOnly(1990, 1, 1), JewishHeritage = heritage, ReligiousBackground = movement, Bio = "I prefer Tevet, with unfamiliar terms explained." },
        }, new DateOnly(2026, 9, 5));

        var context = JsonSerializer.SerializeToElement(personalization.UserContext);

        Assert.AreEqual(movement, context.GetProperty("religiousBackground").GetString());
        Assert.AreEqual(heritage, context.GetProperty("jewishHeritage").GetString());
        StringAssert.Contains(personalization.Instructions, "not a measure of knowledge, observance, or Jewishness");
        StringAssert.Contains(personalization.Instructions, "does not select a single minhag");
        StringAssert.Contains(personalization.Instructions, "explicit preference in additionalContext first");
        Assert.IsFalse(context.TryGetProperty("observanceLevel", out _));
    }

    [TestMethod]
    [DataRow(false, false, false)]
    [DataRow(false, true, true)]
    [DataRow(true, false, true)]
    [DataRow(true, true, true)]
    [TestCategory("Regression")]
    public void TryValidateQuotationLanguages_PreferredEditionOrComparison_DoesNotSubstituteAnAvailableTranslation(bool includePreferredQuotation, bool differentReference, bool expected)
    {
        var english = CreateSegment("English", "en");
        var hebrew = CreateSegment("Hebrew", "he") with { CanonicalReference = differentReference ? "Genesis 1:2" : english.CanonicalReference };
        var packet = new EvidencePacket([Item("E1", english), Item("E2", hebrew)], english.Text.Length + hebrew.Text.Length);
        var quotations = new List<GroundedQuotationDraft> { new() { EvidenceId = "E2", Text = hebrew.Text, Role = "Quotation" } };
        if (includePreferredQuotation)
        {
            quotations.Add(new GroundedQuotationDraft { EvidenceId = "E1", Text = english.Text, Role = "Translation for requested wording comparison" });
        }
        var draft = new GroundedAnswerDraft { Claims = [new GroundedClaimDraft { Text = "An explanation", EvidenceIds = quotations.Select(quote => quote.EvidenceId).ToArray(), Quotations = quotations }], Disagreements = [], Limitations = [], HumanGuidanceRecommended = false };
        var personalization = new ConversationPersonalization("Spanish", "English", null);

        var valid = personalization.TryValidateQuotationLanguages(draft, packet, out var error);

        Assert.AreEqual(expected, valid);
        if (!expected)
        {
            StringAssert.Contains(error, "approved English quotation");
            StringAssert.Contains(error, "explanation in Spanish");
        }
    }

    [TestMethod]
    [TestCategory("Regression")]
    public void TryValidateQuotationLanguages_CalendarAndTechnicalEvidence_AreNotTorahQuotations()
    {
        var source = CreateSegment("English", "en") with { Collection = "Calendar calculations" };
        var packet = new EvidencePacket([Item("E1", source)], source.Text.Length);
        var draft = new GroundedAnswerDraft { Claims = [new GroundedClaimDraft { Text = "A date", EvidenceIds = ["E1"], Quotations = [new GroundedQuotationDraft { EvidenceId = "E1", Text = source.Text, Role = "Calendar fact" }] }], Disagreements = [], Limitations = [], HumanGuidanceRecommended = false };

        Assert.IsTrue(new ConversationPersonalization("Hebrew", "Hebrew", null).TryValidateQuotationLanguages(draft, packet, out _));
        Assert.IsFalse(ConversationPersonalization.IsReligiousSource(source with { Collection = "Technical background" }));
    }

    [TestMethod]
    [DataRow("“This is a newly translated quotation.”", false)]
    [DataRow("\"This is a newly translated quotation.\"", false)]
    [DataRow("«This is a newly translated quotation.»", false)]
    [DataRow("„This is a newly translated quotation.“", false)]
    [DataRow("The text asks the reader to hear and understand.", true)]
    [DataRow("The term “hear” means to listen.", true)]
    [DataRow("The term \"hear\" asks the listener to pay attention, while \"one\" describes unity.", true)]
    [DataRow("„This is a newly translated quotation.”", false)]
    [TestCategory("Regression")]
    public void TryValidateQuotationLanguages_InlineTranslationCannotHideBehindCorrectSourceMetadata(string prose, bool expected)
    {
        var source = CreateSegment("Hebrew", "he") with { Text = "שְׁמַע יִשְׂרָאֵל יְהֹוָה אֱלֹהֵינוּ יְהֹוָה אֶחָד" };
        var packet = new EvidencePacket([Item("E1", source)], source.Text.Length);
        var draft = new GroundedAnswerDraft { Claims = [new GroundedClaimDraft { Text = prose, EvidenceIds = ["E1"], Quotations = [new GroundedQuotationDraft { EvidenceId = "E1", Text = source.Text, Role = "Approved Hebrew quotation" }] }], Disagreements = [], Limitations = [], HumanGuidanceRecommended = false };

        Assert.AreEqual(expected, new ConversationPersonalization("English", "Hebrew", null).TryValidateQuotationLanguages(draft, packet, out _));
    }

    [TestMethod]
    [TestCategory("Regression")]
    public void TryValidateQuotationLanguages_ApprovedInlineQuoteWithTypographyDifferences_IsRecognizedWithoutTranslating()
    {
        var source = CreateSegment("English", "en") with { Text = "HEAR, O ISRAEL: THE LORD OUR GOD, THE LORD IS ONE." };
        var packet = new EvidencePacket([Item("E1", source)], source.Text.Length);
        var draft = new GroundedAnswerDraft { Claims = [new GroundedClaimDraft { Text = "The text says “Hear, O Israel: the LORD our God, the LORD is one.”", EvidenceIds = ["E1"], Quotations = [new GroundedQuotationDraft { EvidenceId = "E1", Text = source.Text, Role = "Quotation" }] }], Disagreements = [], Limitations = [], HumanGuidanceRecommended = false };

        Assert.IsTrue(new ConversationPersonalization("English", "English", null).TryValidateQuotationLanguages(draft, packet, out _));
    }

    [TestMethod]
    [DataRow("English", "en", 0)]
    [DataRow("Hebrew", "he", 1)]
    [DataRow("Spanish", "es", 3)]
    [TestCategory("Unit")]
    public void LanguageRank_IndependentChoices_PrefersQuotationBeforeResponseLanguage(string language, string code, int expected)
    {
        Assert.AreEqual(expected, ConversationPersonalization.LanguageRank(CreateSegment(language, code), new GroundedQuestion { Question = "Question", ConversationLanguage = "Hebrew", QuotationLanguage = "English" }));
    }

    [TestMethod]
    [TestCategory("Regression")]
    public async Task BuildAsync_SingleSlotAndForeignTopHit_EnrichesPreferredQuotationBeforeFillingBudget()
    {
        var english = CreateSegment("English", "en");
        var spanish = CreateSegment("Spanish", "es");
        var retriever = new PairRetriever(spanish);
        var builder = new EvidencePacketBuilder(retriever, new GroundedAnswerOptions { MaximumEvidenceSegments = 1, MaximumEnrichmentHits = 1 });

        var packet = await builder.BuildAsync([new SourceRetrievalHit(english, 100, true)], new GroundedQuestion { Question = "Explain this", ConversationLanguage = "English", QuotationLanguage = "Spanish", SourceKeys = ["collection:Torah"] }, CancellationToken.None);

        Assert.HasCount(1, packet.Items);
        Assert.AreEqual(spanish.SegmentId, packet.Items[0].Source.SegmentId);
        Assert.AreEqual("E1", packet.Items[0].EvidenceId);
        Assert.IsNotNull(retriever.LastQuery);
        CollectionAssert.AreEqual(new[] { "collection:Torah" }, retriever.LastQuery.SourceKeys.ToArray());
    }

    [TestMethod]
    [TestCategory("Regression")]
    public async Task BuildAsync_TranslationInHits_PreservesTopicRankingAndPrefersSelectedLanguageWithinReference()
    {
        var english = CreateSegment("English", "en");
        var spanish = CreateSegment("Spanish", "es");
        var builder = new EvidencePacketBuilder(new PairRetriever(spanish), new GroundedAnswerOptions { MaximumEvidenceSegments = 1, MaximumEnrichmentHits = 0 });

        var packet = await builder.BuildAsync([new SourceRetrievalHit(english, 100, true), new SourceRetrievalHit(spanish, 1, true)], new GroundedQuestion { Question = "Explain this", QuotationLanguage = "Spanish" }, CancellationToken.None);

        Assert.AreEqual(spanish.SegmentId, packet.Items.Single().Source.SegmentId);
    }

    private static SourceSegment CreateSegment(string language, string code) => new()
    {
        SegmentId = $"test:{code}:segment", DocumentId = $"test:{code}", CanonicalReference = "Genesis 1:1", DocumentOrdinal = 0, Text = "A synthetic quotation for selection tests.", Title = "Genesis", HebrewTitle = "בראשית", Language = language, LanguageCode = code, Collection = "Torah", Categories = ["Torah"], Version = "Test edition", License = "CC0", LicenseCategory = SourceLicenseCategory.Cc0, SourceUrl = "https://example.test/Genesis.1.1", FilePath = "Test.md",
    };

    private static EvidenceItem Item(string id, SourceSegment source) => new(id, source, source.Text, false, source.Text.Length);

    private sealed class PairRetriever(SourceSegment source) : ISourceRetriever
    {
        internal SourceRetrievalQuery? LastQuery { get; private set; }
        public Task<IReadOnlyList<SourceRetrievalHit>> SearchAsync(SourceRetrievalQuery query, CancellationToken cancellationToken = default)
        {
            LastQuery = query;
            return Task.FromResult<IReadOnlyList<SourceRetrievalHit>>([new SourceRetrievalHit(source, 1, true)]);
        }
        public Task<IReadOnlyList<SourceSegment>> GetContextAsync(string documentId, int documentOrdinal, int radius, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SourceSegment>>([]);
    }
}
