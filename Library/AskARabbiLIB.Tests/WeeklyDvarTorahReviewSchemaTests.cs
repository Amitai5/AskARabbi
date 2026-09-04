using System.Text.Json;
using AskARabbiLIB.DvarTorah;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class WeeklyDvarTorahReviewSchemaTests
{
    private const string Template = """{"type":"object","properties":{"concerns":{"items":{"properties":{"evidenceIds":{"items":{"type":"string","enum":["placeholder"]}}}}}}}""";

    [TestMethod]
    [TestCategory("Regression")]
    public void ForEvidence_KnownIds_ConstrainsOutputWithoutMutatingTemplate()
    {
        var first = WeeklyDvarTorahReviewSchema.ForEvidence(Template, ["TA", "NB", "TA"]);
        var second = WeeklyDvarTorahReviewSchema.ForEvidence(Template, ["TC"]);

        using var document = JsonDocument.Parse(first.ToString());
        var ids = document.RootElement.GetProperty("properties").GetProperty("concerns").GetProperty("items").GetProperty("properties").GetProperty("evidenceIds").GetProperty("items").GetProperty("enum").EnumerateArray().Select(value => value.GetString()).ToArray();

        CollectionAssert.AreEqual(new[] { "TA", "NB" }, ids);
        StringAssert.Contains(second.ToString(), "\"enum\":[\"TC\"]");
        StringAssert.Contains(Template, "placeholder");
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow("Source text is not an identifier")]
    [DataRow("TA\"")]
    [TestCategory("Regression")]
    public void ForEvidence_InvalidId_RejectsProseInRestrictedSchema(string id)
    {
        Assert.ThrowsExactly<ArgumentException>(() => WeeklyDvarTorahReviewSchema.ForEvidence(Template, [id]));
    }

    [TestMethod]
    [TestCategory("Regression")]
    public void ForEvidence_MissingOrUnboundedIds_RejectsInvalidInput()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => WeeklyDvarTorahReviewSchema.ForEvidence(Template, null!));
        Assert.ThrowsExactly<ArgumentException>(() => WeeklyDvarTorahReviewSchema.ForEvidence(Template, []));
        Assert.ThrowsExactly<ArgumentException>(() => WeeklyDvarTorahReviewSchema.ForEvidence(Template, [new string('A', 65)]));
    }

    [TestMethod]
    [DataRow("{}")]
    [DataRow("null")]
    [DataRow("")]
    [TestCategory("Regression")]
    public void ForEvidence_MissingStructuredConcernContract_FailsClosed(string template)
    {
        Assert.ThrowsExactly<ArgumentException>(() => WeeklyDvarTorahReviewSchema.ForEvidence(template, ["TA"]));
    }
}
