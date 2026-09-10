using System.Reflection;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbi.DvarTorahJob.Tests;

[TestClass]
public sealed class WeeklyDvarTorahPromptContractTests
{
    [TestMethod]
    [DataRow("storyContextClear")]
    [DataRow("argumentHasBeginningMiddleEnd")]
    [DataRow("conclusionReturnsToOpening")]
    [DataRow("openingHookGrounded")]
    [DataRow("quotationsIntegrated")]
    [DataRow("hookTorahBridgeNatural")]
    [DataRow("spokenFlowNatural")]
    [TestCategory("Regression")]
    public void ReviewSchema_EditorialGate_IsRequiredBooleanWithMatchingInstruction(string property)
    {
        using var schema = JsonDocument.Parse(ReadPrompt("review.schema.json"));

        var definition = schema.RootElement.GetProperty("properties").GetProperty(property);
        var required = schema.RootElement.GetProperty("required").EnumerateArray().Select(item => item.GetString()).ToArray();
        var concernChecks = schema.RootElement.GetProperty("properties").GetProperty("concerns").GetProperty("items").GetProperty("properties").GetProperty("check").GetProperty("enum").EnumerateArray().Select(item => item.GetString()).ToArray();

        Assert.AreEqual("boolean", definition.GetProperty("type").GetString());
        CollectionAssert.Contains(required, property);
        CollectionAssert.Contains(concernChecks, char.ToUpperInvariant(property[0]) + property[1..]);
        StringAssert.Contains(ReadPrompt("review-system.txt"), $"Set {property} true only when");
    }

    [TestMethod]
    [TestCategory("Regression")]
    public void WritingContract_BeginnerEssay_PreservesGroundingAndDefinesTheNarrativeArc()
    {
        var draft = ReadPrompt("draft-system.txt");
        var research = ReadPrompt("research-system.txt");

        StringAssert.Contains(draft, "has NOT read the parashah");
        StringAssert.Contains(draft, "BEGINNING:");
        StringAssert.Contains(draft, "first model-generated paragraph must open with a compelling modern hook");
        StringAssert.Contains(draft, "By the second or third model-generated paragraph, explicitly name");
        StringAssert.Contains(draft, "Do not substitute vague labels");
        StringAssert.Contains(draft, "MIDDLE:");
        StringAssert.Contains(draft, "END:");
        StringAssert.Contains(draft, "introductionAddedByApplication");
        StringAssert.Contains(draft, "Use only supplied evidence IDs");
        StringAssert.Contains(draft, "application, not you, inserts exact licensed Torah quotations");
        StringAssert.Contains(draft, "you do not need to cite every available Torah passage");
        StringAssert.Contains(research, "speakers, their relationships, and what has just happened");
    }

    [TestMethod]
    [TestCategory("Regression")]
    public void ReviewFeedback_ReportsDefectsWithoutRepeatingSourceMaterial()
    {
        var review = ReadPrompt("review-system.txt");

        StringAssert.Contains(review, "Do not quote or copy any wording");
        StringAssert.Contains(review, "Return no free-form explanation or prose");
        StringAssert.Contains(review, "Review every check fully");
        StringAssert.Contains(review, "safeToPublish true only when every other requirement passes");
    }

    [TestMethod]
    [TestCategory("Regression")]
    public void WritingContract_ModernHookAndInlineQuotes_RequiresSupportedDetailsAndActionableReturn()
    {
        var draft = ReadPrompt("draft-system.txt");
        var research = ReadPrompt("research-system.txt");
        var review = ReadPrompt("review-system.txt");

        StringAssert.Contains(draft, "{{quote:TA}}");
        StringAssert.Contains(draft, "in the SAME paragraph");
        StringAssert.Contains(draft, "Never put a slot on its own line");
        StringAssert.Contains(draft, "Make the first step doable today");
        StringAssert.Contains(draft, "does not waive research requirements");
        StringAssert.Contains(draft, "Do not invent a study");
        StringAssert.Contains(research, "do not supply movie plots, popularity claims, study findings, or numbers from memory");
        StringAssert.Contains(review, "Do not require Torah character names in the modern hook itself");
        StringAssert.Contains(review, "an occasion and a concrete action the reader could take today");
    }

    [TestMethod]
    [TestCategory("Regression")]
    public void ResearchContract_ContemporaryLens_RequiresConstructiveSingleEventCorroboration()
    {
        var research = ReadPrompt("research-system.txt");

        StringAssert.Contains(research, "constructive, nonpolitical development");
        StringAssert.Contains(research, "every selected item must describe the same specific development");
        StringAssert.Contains(research, "If such corroboration is unavailable, do not invent it");
        StringAssert.Contains(research, "Do not use a multi-topic newsletter or news roundup");
    }

    [TestMethod]
    [TestCategory("Regression")]
    public void ReviewSchema_ConcernFields_ContainNoFreeFormText()
    {
        using var schema = JsonDocument.Parse(ReadPrompt("review.schema.json"));
        var concern = schema.RootElement.GetProperty("properties").GetProperty("concerns").GetProperty("items");
        var properties = concern.GetProperty("properties");

        Assert.AreEqual(3, properties.EnumerateObject().Count());
        Assert.AreEqual("string", properties.GetProperty("check").GetProperty("type").GetString());
        Assert.AreEqual(26, properties.GetProperty("check").GetProperty("enum").GetArrayLength());
        Assert.AreEqual("array", properties.GetProperty("evidenceIds").GetProperty("type").GetString());
        Assert.IsTrue(properties.GetProperty("evidenceIds").GetProperty("items").TryGetProperty("enum", out _));
        Assert.AreEqual("integer", properties.GetProperty("paragraphIndex").GetProperty("type").GetString());
        Assert.IsFalse(concern.GetProperty("additionalProperties").GetBoolean());
    }

    [TestMethod]
    [TestCategory("Regression")]
    public void WritingContract_ContinuousSpeech_RequiresBridgeProgressionAndReadAloudPass()
    {
        var draft = ReadPrompt("draft-system.txt");
        var research = ReadPrompt("research-system.txt");
        var repair = ReadPrompt("repair.txt");

        StringAssert.Contains(research, "concrete human decision shared by the contemporary situation and the Torah question");
        StringAssert.Contains(research, "proposed directions for retrieval, not established Torah interpretations");
        StringAssert.Contains(draft, "carry that SAME question into the Torah scene before the first quotation");
        StringAssert.Contains(draft, "A news report followed by");
        StringAssert.Contains(draft, "brief Torah scene that sharpens it");
        StringAssert.Contains(draft, "not an outline to copy into the body paragraph by paragraph");
        StringAssert.Contains(draft, "previous paragraph's discovery or unresolved question");
        StringAssert.Contains(draft, "never invent a commentator's position");
        StringAssert.Contains(draft, "plain explanations of what people face or do");
        StringAssert.Contains(draft, "not an academic describing a text's argument");
        StringAssert.Contains(draft, "not worksheet labels");
        StringAssert.Contains(draft, "Do not reserve a paragraph for summarizing all remaining verses");
        StringAssert.Contains(draft, "not a claim that a Torah verse specifically commands");
        StringAssert.Contains(draft, "Keep exact quotation wording unchanged");
        StringAssert.Contains(draft, "silent read-aloud editing pass from the fixed welcome through the closing");
        StringAssert.Contains(repair, "revise the connected passage, not just a transition word");
        StringAssert.Contains(repair, "all grounding, safety, source, and count requirements");
    }

    [TestMethod]
    [TestCategory("Regression")]
    public void ReviewContract_GroundedButDisconnectedSpeech_StillFailsEditorialChecks()
    {
        var review = ReadPrompt("review-system.txt");

        StringAssert.Contains(review, "accurate news alone cannot pass that check");
        StringAssert.Contains(review, "A shared abstract topic is not enough");
        StringAssert.Contains(review, "a connection explained only near the end");
        StringAssert.Contains(review, "individually sound but disconnected verse summaries");
        StringAssert.Contains(review, "Having an introduction, body, and conclusion alone is not enough to pass");
        StringAssert.Contains(review, "without demanding that the quotation be modernized");
        StringAssert.Contains(review, "even if it has a bridge and sound citations");
        StringAssert.Contains(review, "a paragraph that lists leftover verses solely to meet source counts also fail");
        StringAssert.Contains(review, "Judge patterns that materially burden a first-time listener");
    }

    private static string ReadPrompt(string fileName)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"Prompts.{fileName}") ?? throw new AssertFailedException($"Missing embedded prompt {fileName}.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
