using System.Text.Json;
using AskARabbiLIB.AI.Tools;
using AskARabbiLIB.Grounding;
using AskARabbiLIB.Lexicon;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class BdbDictionaryAIToolsTests
{
    private static AIToolExecutionContext Context => new(null, new DateTimeOffset(2026, 9, 12, 12, 0, 0, TimeSpan.Zero));

    [TestMethod]
    public void Registry_BdbProvider_ExposesSearchAndReadWithoutDatabaseDetails()
    {
        var registry = new AIToolRegistry([new BdbDictionaryAITools(new LexiconTestData.Store())]);

        CollectionAssert.AreEquivalent(new[] { "search_bdb_dictionary", "read_bdb_entry" }, registry.Definitions.Select(definition => definition.Name).ToArray());
        Assert.IsTrue(registry.MayApply("What does this Hebrew word mean?"));
        Assert.IsFalse(registry.Definitions.Any(definition => definition.ParametersJsonSchema.ToString().Contains("ConnectionString", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ExecuteAsync_MultipleArticles_AssignsDistinctEvidenceAndRetainsDictionaryAttribution()
    {
        var registry = new AIToolRegistry([new BdbDictionaryAITools(new LexiconTestData.Store(LexiconTestData.Entry(), LexiconTestData.Entry("BDB00002")))]);
        var session = new AIToolExecutionSession(registry, Context, 2);

        var output = await session.ExecuteAsync("search_bdb_dictionary", BinaryData.FromString("{\"query\":\"קרא\"}"));
        using var json = JsonDocument.Parse(output);

        Assert.IsTrue(json.RootElement.GetProperty("isSuccess").GetBoolean());
        Assert.AreEqual(2, json.RootElement.GetProperty("evidence").GetArrayLength());
        CollectionAssert.AreEqual(new[] { "E3", "E4" }, session.EvidenceItems.Select(item => item.EvidenceId).ToArray());
        Assert.AreEqual("Dictionaries", session.EvidenceItems[0].Source.Collection);
        Assert.AreEqual(LexiconTestData.Entry().SourceUrl, session.EvidenceItems[0].Source.SourceUrl);
        Assert.AreEqual(LexiconTestData.Entry().Text, session.EvidenceItems[0].PresentedText);
        Assert.IsFalse(ConversationPersonalization.IsReligiousSource(session.EvidenceItems[0].Source));
    }

    [TestMethod]
    public async Task SearchAsync_LongArticle_ReturnsContiguousRelevantExcerptAndContinuation()
    {
        var entry = LexiconTestData.Entry() with { Headword = "אחר", Text = new string('x', 4000) + " קָרָא call and read. " + new string('y', 4000) };
        var tool = new BdbDictionaryAITools(new LexiconTestData.Store(entry));

        var result = await tool.SearchAsync("קרא");
        var source = result.Sources.Single();

        Assert.IsTrue(source.IsExcerpt);
        Assert.IsGreaterThan(0, source.ExcerptStart);
        StringAssert.Contains(source.Text, "קָרָא");
        Assert.AreEqual(entry.Text.Substring(source.ExcerptStart, source.Text.Length), source.Text);
        Assert.IsLessThanOrEqualTo(2400, source.Text.Length);
        var next = await tool.ReadAsync(entry.EntryId, source.ExcerptStart + source.Text.Length);
        Assert.IsTrue(next.IsSuccess);
        Assert.AreEqual(entry.Text.Substring(next.Sources[0].ExcerptStart, next.Sources[0].Text.Length), next.Sources[0].Text);
    }

    [TestMethod]
    public async Task SearchAsync_LateSubentryOpening_PrefersItToAnEarlierPassingMention()
    {
        var entry = LexiconTestData.Entry() with { Text = "Unrelated introduction " + new string('x', 500) + " read " + new string('y', 3000) + "\nקָרָא read, call, proclaim." };
        var tool = new BdbDictionaryAITools(new LexiconTestData.Store(entry));

        var result = await tool.SearchAsync("read");

        var source = result.Sources.Single();
        Assert.IsGreaterThan(3000, source.ExcerptStart);
        StringAssert.Contains(source.Text, "קָרָא read, call, proclaim.");
    }

    [TestMethod]
    public async Task SearchAsync_NoMatch_DoesNotCreateEvidenceOrAssertWordDoesNotExist()
    {
        var result = await new BdbDictionaryAITools(new LexiconTestData.Store()).SearchAsync("unknown");

        Assert.IsFalse(result.IsSuccess);
        Assert.HasCount(0, result.Sources);
        StringAssert.Contains(result.ErrorMessage, "not evidence");
    }

    [TestMethod]
    public async Task ExecuteAsync_UnavailableStorage_ReturnsExplicitFailureWithoutInventingEvidence()
    {
        var registry = new AIToolRegistry([new BdbDictionaryAITools(new UnavailableLexiconStore())]);
        var session = new AIToolExecutionSession(registry, Context, 0);

        var output = await session.ExecuteAsync("search_bdb_dictionary", BinaryData.FromString("{\"query\":\"read\"}"));

        Assert.HasCount(0, session.EvidenceItems);
        StringAssert.Contains(output.ToString(), "not available");
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(999999)]
    public async Task ReadAsync_InvalidOffset_ReturnsFailure(int offset)
    {
        var result = await new BdbDictionaryAITools(new LexiconTestData.Store(LexiconTestData.Entry())).ReadAsync("BDB00001", offset);

        Assert.IsFalse(result.IsSuccess);
        Assert.HasCount(0, result.Sources);
    }

    [TestMethod]
    public async Task ExecuteAsync_CallBudget_StopsFurtherDatabaseCalls()
    {
        var store = new LexiconTestData.Store(LexiconTestData.Entry());
        var session = new AIToolExecutionSession(new AIToolRegistry([new BdbDictionaryAITools(store)]), Context, 0, 1);
        await session.ExecuteAsync("search_bdb_dictionary", BinaryData.FromString("{\"query\":\"read\"}"));

        var output = await session.ExecuteAsync("search_bdb_dictionary", BinaryData.FromString("{\"query\":\"read\"}"));

        Assert.AreEqual(1, store.Calls);
        StringAssert.Contains(output.ToString(), "maximum number");
    }

    [TestMethod]
    public async Task SearchAsync_CancelledRequest_PropagatesCancellation()
    {
        await Assert.ThrowsAsync<OperationCanceledException>(() => new BdbDictionaryAITools(new LexiconTestData.Store()).SearchAsync("read", new CancellationToken(true)));
    }
}
