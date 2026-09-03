using AskARabbiLIB.Grounding;
using AskARabbiLIB.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class GroundedAnswerTextRendererTests
{
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
