using AskARabbiLIB.Grounding;
using AskARabbiLIB.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class GroundedAnswerTextRendererTests
{
    [TestMethod]
    [DataRow("English", "Another perspective:", "Some quotations")]
    [DataRow("French", "Une autre perspective :", "Certaines citations")]
    [DataRow("German", "Eine andere Perspektive:", "Einige Zitate")]
    [DataRow("Hebrew", "נקודת מבט נוספת:", "חלק מהציטוטים")]
    [DataRow("Italian", "Un'altra prospettiva:", "Alcune citazioni")]
    [DataRow("Persian", "دیدگاهی دیگر:", "برخی نقل‌قول‌ها")]
    [DataRow("Polish", "Inna perspektywa:", "Niektóre cytaty")]
    [DataRow("Russian", "Другая точка зрения:", "Некоторые цитаты")]
    [DataRow("Spanish", "Otra perspectiva:", "Algunas citas")]
    [DataRow("Yiddish", "אַן אַנדער בליקווינקל:", "עטלעכע ציטאַטן")]
    [TestCategory("Regression")]
    public void Render_AllResponseLanguages_LocalizesApplicationTextAndEditionFallback(string language, string perspective, string fallback)
    {
        var answer = new GroundedAnswer([new GroundedClaim("Synthetic claim", [CreateCitation()], null, null)], [new GroundedDisagreement("Synthetic perspective", [CreateCitation()])], [], "Synthetic follow-up", true, [CreateCitation()])
        {
            InterpretiveNotice = string.Empty, ResponseLanguage = language, QuotationLanguage = "Hebrew",
        };

        var rendered = new GroundedAnswerTextRenderer().Render(answer);

        StringAssert.Contains(rendered, perspective);
        StringAssert.Contains(rendered, fallback);
        StringAssert.Contains(rendered, ConversationPresentationText.ForLanguage(language).Continuation);
        StringAssert.Contains(rendered, ConversationPresentationText.ForLanguage(language).Guidance);
        if (language != "English")
        {
            Assert.IsFalse(rendered.Contains("Another perspective:", StringComparison.Ordinal));
            Assert.IsFalse(rendered.Contains("qualified rabbi", StringComparison.Ordinal));
        }
    }

    [TestMethod]
    [DataRow("Calendar calculations")]
    [DataRow("Technical background")]
    [TestCategory("Regression")]
    public void Render_NonReligiousEvidence_DoesNotWarnAboutTorahEdition(string collection)
    {
        var citation = CreateCitation() with { Collection = collection };
        var answer = new GroundedAnswer([new GroundedClaim("Calculated fact", [citation], null, null)], [], [], null, false, [citation]) { InterpretiveNotice = string.Empty, QuotationLanguage = "Hebrew" };

        Assert.AreEqual("Calculated fact [1]", new GroundedAnswerTextRenderer().Render(answer));
    }

    [TestMethod]
    [TestCategory("Regression")]
    public void Render_RequestedWordingComparison_DoesNotFalselyClaimPreferredEditionUnavailable()
    {
        var english = CreateCitation();
        var hebrew = english with { Number = 2, Language = "Hebrew", LanguageCode = "he", SegmentId = "he:1" };
        var answer = new GroundedAnswer([new GroundedClaim("A requested comparison", [english, hebrew], null, null)], [], [], null, false, [english, hebrew]) { InterpretiveNotice = string.Empty, QuotationLanguage = "English" };

        Assert.AreEqual("A requested comparison [1] [2]", new GroundedAnswerTextRenderer().Render(answer));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Render_CompleteValidatedAnswer_ProducesConversationWithoutInternalLimitationsOrStockNotice()
    {
        var citation = CreateCitation();
        var quotation = new GroundedQuotation("A tested quotation.", "Supports the explanation.", citation);
        var answer = new GroundedAnswer(
            [new GroundedClaim("The short answer is grounded.", [citation], null, null) { Quotations = [quotation] }],
            [new GroundedDisagreement("Another authority reads it differently.", [citation]) { Quotations = [quotation] }],
            ["The available sources do not decide every modern case."],
            "Would you like to compare another opinion?",
            true,
            [citation])
        {
            InterpretiveNotice = "This is source-based learning, not personal psak.",
        };

        var rendered = new GroundedAnswerTextRenderer().Render(answer);

        StringAssert.StartsWith(rendered, "The short answer is grounded. [1]");
        StringAssert.Contains(rendered, "Another perspective:");
        Assert.IsFalse(rendered.Contains("What these sources do not fully answer:", StringComparison.Ordinal));
        StringAssert.Contains(rendered, "If you'd like to keep exploring:");
        Assert.IsFalse(rendered.Contains("Ask me that next", StringComparison.Ordinal));
        StringAssert.Contains(rendered, "qualified rabbi");
        Assert.IsFalse(rendered.Contains("This is source-based learning, not personal psak.", StringComparison.Ordinal));
        Assert.IsFalse(rendered.Contains("A tested quotation.", StringComparison.Ordinal));
        Assert.IsFalse(rendered.Contains("https://www.sefaria.org", StringComparison.Ordinal));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Render_MinimalAnswer_OmitsOptionalSectionsAndExtraSpacing()
    {
        var answer = new GroundedAnswer([new GroundedClaim("  Direct answer.  ", [], null, null)], [], [], " ", false, [])
        {
            InterpretiveNotice = "  Keep asking questions.  ",
        };

        var rendered = new GroundedAnswerTextRenderer().Render(answer);

        Assert.AreEqual("Direct answer.", rendered);
        Assert.IsFalse(rendered.Contains("Another perspective", StringComparison.Ordinal));
        Assert.IsFalse(rendered.Contains("qualified rabbi", StringComparison.Ordinal));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Render_NullAnswer_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new GroundedAnswerTextRenderer().Render(null!));
    }

    private static SourceCitation CreateCitation() => new(
        1,
        "E1",
        "sefaria:document:segment:00000001",
        "Genesis",
        "בראשית",
        "Genesis 1:1",
        "Test edition",
        "English",
        "en",
        "Torah",
        ["Tanakh", "Torah"],
        "CC-BY",
        SourceLicenseCategory.CcBy,
        "https://www.sefaria.org/Genesis.1.1",
        "Data/NormalizedData/Sefaria/Torah/Genesis.md",
        false);

}
