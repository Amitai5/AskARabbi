using System.Text.Json;
using AskARabbiLIB.AI.Tools;
using AskARabbiLIB.Grounding;
using AskARabbiLIB.Retrieval;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class SourceResearchAIToolsTests
{
    [TestMethod]
    public async Task SearchAsync_ModelBroadensToHoliday_DoesNotLoseOriginalFoodQuestion()
    {
        var sources = new SourceResearchTestData.Sources { Results = [SourceResearchTestData.Passage("Rosh Hashanah prayers and shofar obligations are discussed.")] };
        var research = new SourceResearchAITools(sources, sources);
        var context = SourceResearchTestData.Context with { SourceFilters = new AskARabbiLIB.Retrieval.SourceRetrievalQuery { QueryText = SourceResearchTestData.Question } };

        var result = await research.SearchAsync("Rosh Hashanah", context);

        Assert.IsFalse(result.IsSuccess);
        Assert.IsEmpty(result.Sources);
    }

    [TestMethod]
    public async Task ExecuteAsync_FirstReadFails_ReleasesRequiredChoiceWithoutResettingBudget()
    {
        var sources = new SourceResearchTestData.Sources { Empty = true };
        var registry = new AIToolRegistry([new SourceResearchAITools(sources, sources)]);
        var session = new AIToolExecutionSession(registry, SourceResearchTestData.Context, 0, initialRequiredToolName: "read_source_passage");
        Assert.AreEqual("read_source_passage", session.RequiredToolName);

        await session.ExecuteAsync("read_source_passage", BinaryData.FromString("{\"reference\":\"Genesis 1:1\"}"));

        Assert.IsNull(session.RequiredToolName);
        Assert.AreEqual(1, session.ExecutionCount);
        Assert.AreEqual(4, session.MaximumExecutionCount);
        Assert.ThrowsExactly<ArgumentException>(() => new AIToolExecutionSession(registry, SourceResearchTestData.Context, 0, initialRequiredToolName: "missing"));
    }

    [TestMethod]
    public void Evaluate_SquashQuestionWithOnlyHolidayPrayerMatches_RejectsUnrelatedEvidence()
    {
        var hits = new[] { new SourceRetrievalHit(SourceResearchTestData.Passage("The Rosh Hashanah Musaf prayers and shofar obligations are discussed."), 1, false) };

        var result = SourceEvidenceAdequacyEvaluator.Evaluate(SourceResearchTestData.Question, hits);

        Assert.IsFalse(result.IsAdequate);
    }

    [TestMethod]
    [DataRow("squash")]
    [DataRow("pumpkin")]
    [DataRow("gourd")]
    public void Evaluate_AlternateFoodNames_RecognizesActualCustom(string food)
    {
        var hits = new[] { new SourceRetrievalHit(SourceResearchTestData.Passage(), 1, false) };

        var result = SourceEvidenceAdequacyEvaluator.Evaluate($"Why {food} at Rosh Hashanah?", hits);

        Assert.IsTrue(result.IsAdequate);
    }

    [TestMethod]
    public async Task SearchAsync_FocusedResearch_PreservesTrustedFiltersAndProvenance()
    {
        var sources = new SourceResearchTestData.Sources();
        var research = new SourceResearchAITools(sources, sources);

        var result = await research.SearchAsync("gourd Rosh Hashanah prayer", SourceResearchTestData.Context);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(SourceResearchTestData.Reference, result.Sources[0].CanonicalReference);
        Assert.AreEqual(SourceResearchTestData.Passage().Text, result.Sources[0].Text);
        CollectionAssert.AreEqual(SourceResearchTestData.Context.SourceFilters!.SourceKeys.ToArray(), sources.Searches[0].SourceKeys.ToArray());
        CollectionAssert.AreEqual(new[] { "English", "Hebrew" }, sources.Searches[0].Languages.ToArray());
        Assert.AreEqual(12, sources.Searches[0].CandidateLimit);
    }

    [TestMethod]
    public async Task ExecuteAsync_ModelAttemptsToChangeSources_RejectsBeforeReading()
    {
        var sources = new SourceResearchTestData.Sources();
        var registry = new AIToolRegistry([new SourceResearchAITools(sources, sources)]);

        var result = await registry.ExecuteAsync("search_source_passages", BinaryData.FromString("{\"query\":\"gourd\",\"sourceKeys\":[]}"), SourceResearchTestData.Context);

        Assert.IsFalse(result.IsSuccess);
        Assert.IsEmpty(sources.Searches);
    }

    [TestMethod]
    public async Task SearchAsync_MissingTrustedScope_DoesNotReadCorpus()
    {
        var sources = new SourceResearchTestData.Sources();
        var research = new SourceResearchAITools(sources, sources);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => research.SearchAsync("gourd", new AIToolExecutionContext(null, SourceResearchTestData.Now)));

        Assert.IsEmpty(sources.Searches);
    }

    [TestMethod]
    public async Task SearchAsync_QueryTooLong_DoesNotReadCorpus()
    {
        var sources = new SourceResearchTestData.Sources();
        var research = new SourceResearchAITools(sources, sources);

        var result = await research.SearchAsync(new string('x', 401), SourceResearchTestData.Context);

        Assert.IsFalse(result.IsSuccess);
        Assert.IsEmpty(sources.Searches);
    }

    [TestMethod]
    [DataRow("https://example.com/583", 0)]
    [DataRow("../private 1", 0)]
    [DataRow("Shabbat 31a", -1)]
    [DataRow("not a reference", 0)]
    public async Task ReadAsync_InvalidReferenceOrOffset_DoesNotReadCorpus(string reference, int offset)
    {
        var sources = new SourceResearchTestData.Sources();
        var research = new SourceResearchAITools(sources, sources);

        var result = await research.ReadAsync(reference, SourceResearchTestData.Context, offset);

        Assert.IsFalse(result.IsSuccess);
        Assert.IsEmpty(sources.Reads);
    }

    [TestMethod]
    public async Task ReadAsync_LongPassage_ReturnsBoundedExactContinuations()
    {
        var text = new string('a', 2_399) + "🌿" + new string('b', 200);
        var sources = new SourceResearchTestData.Sources { Results = [SourceResearchTestData.Passage(text)] };
        var research = new SourceResearchAITools(sources, sources);

        var first = await research.ReadAsync(SourceResearchTestData.Reference, SourceResearchTestData.Context);
        var metadata = JsonSerializer.SerializeToElement(first.Data);
        var offset = metadata.GetProperty("passages")[0].GetProperty("nextOffset").GetInt32();
        var second = await research.ReadAsync(SourceResearchTestData.Reference, SourceResearchTestData.Context, offset);

        Assert.AreEqual(2_399, offset);
        Assert.AreEqual(text, first.Sources[0].Text + second.Sources[0].Text);
        Assert.AreEqual(offset, second.Sources[0].ExcerptStart);
        Assert.IsTrue(first.Sources[0].IsExcerpt);
        CollectionAssert.AreEqual(new[] { "English", "Hebrew" }, sources.Reads[0].Filters.Languages.ToArray());
    }

    [TestMethod]
    public async Task ReadAsync_Range_IsBoundedAndIdentifiesNextReference()
    {
        var sources = new SourceResearchTestData.Sources { Results = Enumerable.Range(1, 5).Select(number => SourceResearchTestData.Passage(new string('x', 4_000), $"Genesis 1:{number}")).ToArray() };
        var research = new SourceResearchAITools(sources, sources);

        var result = await research.ReadAsync("Genesis 1", SourceResearchTestData.Context);

        Assert.HasCount(3, result.Sources);
        Assert.AreEqual(7_200, result.Sources.Sum(source => source.Text.Length));
        Assert.AreEqual("Genesis 1:4", JsonSerializer.SerializeToElement(result.Data).GetProperty("nextReference").GetString());
    }

    [TestMethod]
    public async Task ExecuteAsync_ResearchBudgetExhausted_DoesNotMakeFifthRead()
    {
        var sources = new SourceResearchTestData.Sources();
        var session = new AIToolExecutionSession(new AIToolRegistry([new SourceResearchAITools(sources, sources)]), SourceResearchTestData.Context, 0);
        var arguments = BinaryData.FromString("{\"reference\":\"Shulchan Arukh, Orach Chayim 583:1\"}");

        for (var index = 0; index < 4; index++)
        {
            await session.ExecuteAsync("read_source_passage", arguments);
        }
        using var failure = JsonDocument.Parse(await session.ExecuteAsync("read_source_passage", arguments));

        Assert.IsFalse(failure.RootElement.GetProperty("isSuccess").GetBoolean());
        Assert.HasCount(4, sources.Reads);
    }

    [TestMethod]
    public async Task ReadAsync_UnavailableReference_ReturnsNoEvidence()
    {
        var sources = new SourceResearchTestData.Sources { Empty = true };
        var research = new SourceResearchAITools(sources, sources);

        var result = await research.ReadAsync("Genesis 1:1", SourceResearchTestData.Context);

        Assert.IsFalse(result.IsSuccess);
        Assert.IsEmpty(result.Sources);
    }

    [TestMethod]
    public async Task SearchAsync_Cancelled_PropagatesCancellation()
    {
        var sources = new SourceResearchTestData.Sources();
        var research = new SourceResearchAITools(sources, sources);

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => research.SearchAsync("gourd", SourceResearchTestData.Context, new CancellationToken(true)));

        Assert.IsEmpty(sources.Searches);
    }
}
