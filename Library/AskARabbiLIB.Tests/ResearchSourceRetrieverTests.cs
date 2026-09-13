using AskARabbiLIB.Retrieval;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class ResearchSourceRetrieverTests
{
    [TestMethod]
    public async Task SearchAsync_SemanticMiss_UsesOriginalQuestionAndUnchangedSourceFilters()
    {
        var semantic = new SourceResearchTestData.Sources { InitialMiss = true };
        var keywords = new SourceResearchTestData.Sources();
        var filters = SourceResearchTestData.Context.SourceFilters ?? throw new AssertFailedException("Missing fixture scope.");
        var query = filters with { QueryText = SourceResearchTestData.Question, CandidateLimit = 10 };

        var hits = await new ResearchSourceRetriever(semantic, keywords).SearchAsync(query);

        Assert.HasCount(1, keywords.Searches);
        Assert.AreSame(query, keywords.Searches[0]);
        Assert.AreEqual(SourceResearchTestData.Reference, hits[0].Segment.CanonicalReference);
    }

    [TestMethod]
    public async Task SearchAsync_RelevantSemanticHits_DoesNotRepeatSearch()
    {
        var semantic = new SourceResearchTestData.Sources();
        var keywords = new ContextRetriever();

        var hits = await new ResearchSourceRetriever(semantic, keywords).SearchAsync(new SourceRetrievalQuery { QueryText = SourceResearchTestData.Question });

        Assert.HasCount(1, hits);
        Assert.AreEqual(0, keywords.ContextCalls);
    }

    [TestMethod]
    public async Task SearchAsync_NoRelevantKeywordResults_DoesNotTurnUnrelatedTextIntoSupport()
    {
        var semantic = new SourceResearchTestData.Sources { InitialMiss = true };
        var keywords = new SourceResearchTestData.Sources { InitialMiss = true };

        var hits = await new ResearchSourceRetriever(semantic, keywords).SearchAsync(new SourceRetrievalQuery { QueryText = SourceResearchTestData.Question });

        Assert.HasCount(1, keywords.Searches);
        Assert.AreEqual("Shulchan Arukh, Orach Chayim 591:1", hits[0].Segment.CanonicalReference);
        Assert.IsFalse(AskARabbiLIB.Grounding.SourceEvidenceAdequacyEvaluator.Evaluate(SourceResearchTestData.Question, hits).IsAdequate);
    }

    [TestMethod]
    public async Task SearchAsync_Cancellation_DoesNotRunFallback()
    {
        var keywords = new ContextRetriever();
        var retriever = new ResearchSourceRetriever(new SourceResearchTestData.Sources(), keywords);

        await Assert.ThrowsAsync<OperationCanceledException>(() => retriever.SearchAsync(new SourceRetrievalQuery { QueryText = SourceResearchTestData.Question }, new CancellationToken(true)));

        Assert.AreEqual(0, keywords.ContextCalls);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task GetContextAsync_LocalEditionAvailable_AvoidsNetworkContextLookup(bool available)
    {
        IReadOnlyList<SourceSegment> passages = [SourceResearchTestData.Passage()];
        var semantic = new ContextRetriever { Context = passages };
        var keywords = new ContextRetriever { Context = available ? passages : [] };

        var result = await new ResearchSourceRetriever(semantic, keywords).GetContextAsync("document", 3, 1);

        Assert.AreSame(passages, result);
        Assert.AreEqual(available ? 0 : 1, semantic.ContextCalls);
    }

    private sealed class ContextRetriever : ISourceRetriever
    {
        internal int ContextCalls { get; private set; }
        internal IReadOnlyList<SourceSegment> Context { get; init; } = [];
        public Task<IReadOnlyList<SourceRetrievalHit>> SearchAsync(SourceRetrievalQuery query, CancellationToken cancellationToken = default) => throw new AssertFailedException("Unexpected fallback search.");

        public Task<IReadOnlyList<SourceSegment>> GetContextAsync(string documentId, int documentOrdinal, int radius, CancellationToken cancellationToken = default)
        {
            Assert.AreEqual("document", documentId);
            Assert.AreEqual(3, documentOrdinal);
            Assert.AreEqual(1, radius);
            ContextCalls++;
            return Task.FromResult(Context);
        }
    }
}
