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
    [TestCategory("Regression")]
    public void ReviewSchema_EditorialGate_IsRequiredBooleanWithMatchingInstruction(string property)
    {
        using var schema = JsonDocument.Parse(ReadPrompt("review.schema.json"));

        var definition = schema.RootElement.GetProperty("properties").GetProperty(property);
        var required = schema.RootElement.GetProperty("required").EnumerateArray().Select(item => item.GetString()).ToArray();

        Assert.AreEqual("boolean", definition.GetProperty("type").GetString());
        CollectionAssert.Contains(required, property);
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
        Assert.AreEqual(22, properties.GetProperty("check").GetProperty("enum").GetArrayLength());
        Assert.AreEqual("array", properties.GetProperty("evidenceIds").GetProperty("type").GetString());
        Assert.IsTrue(properties.GetProperty("evidenceIds").GetProperty("items").TryGetProperty("enum", out _));
        Assert.AreEqual("integer", properties.GetProperty("paragraphIndex").GetProperty("type").GetString());
        Assert.IsFalse(concern.GetProperty("additionalProperties").GetBoolean());
    }

    private static string ReadPrompt(string fileName)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"Prompts.{fileName}") ?? throw new AssertFailedException($"Missing embedded prompt {fileName}.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
