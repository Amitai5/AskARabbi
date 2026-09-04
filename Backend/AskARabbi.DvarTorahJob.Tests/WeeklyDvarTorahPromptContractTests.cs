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
        StringAssert.Contains(research, "speakers, their relationships, and what has just happened");
    }

    private static string ReadPrompt(string fileName)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"Prompts.{fileName}") ?? throw new AssertFailedException($"Missing embedded prompt {fileName}.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
