using AskARabbiLIB.Conversations;
using AskARabbiLIB.Grounding;
using AskARabbiLIB.Models;
using AskARabbiLIB.Retrieval;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class TeachingReferenceGuideTests
{
    [TestMethod]
    [DataRow(null, "Genesis 1:1|Deuteronomy 30:19")]
    [DataRow("Choose life [2].", "Deuteronomy 30:19")]
    [DataRow("Choose life.", "Genesis 1:1|Deuteronomy 30:19")]
    [TestCategory("Regression")]
    public async Task ReadAsync_TeachingSelection_RoutesReferencesAndPreservesSourceFilters(string? selection, string expected)
    {
        var reader = new RecordingReader();
        var question = new GroundedQuestion { Question = "Explain this teaching.", Languages = ["Hebrew"], TeachingContext = new("diaspora:2026-08-29", "Choose life", "Choose life [2].", "[1] Genesis 1:1 — https://example.test/one\n[2] Deuteronomy 30:19 — https://example.test/two", selection) };
        var query = new SourceRetrievalQuery { SourceKeys = ["collection:Torah"], Collections = ["Torah"], CandidateLimit = 12 };

        await ConversationReferenceGuide.ReadAsync(reader, question, [], query, CancellationToken.None);

        CollectionAssert.AreEqual(expected.Split('|'), reader.Calls.Select(call => call.Reference).ToArray());
        foreach (var call in reader.Calls)
        {
            CollectionAssert.AreEqual(new[] { "collection:Torah" }, call.Filters.SourceKeys.ToArray());
            CollectionAssert.AreEqual(new[] { "Torah" }, call.Filters.Collections.ToArray());
            CollectionAssert.AreEqual(new[] { "Hebrew" }, call.Filters.Languages.ToArray());
            Assert.AreEqual(12, call.Filters.CandidateLimit);
        }
    }

    [TestMethod]
    [TestCategory("Regression")]
    public async Task ReadAsync_UserNamesDifferentReference_PrioritizesTheUserQuestion()
    {
        var reader = new RecordingReader();
        var question = new GroundedQuestion { Question = "How does Exodus 35:3 relate?", TeachingContext = new("diaspora:2026-08-29", "Choose life", "Choose life.", "[1] Deuteronomy 30:19", null) };

        await ConversationReferenceGuide.ReadAsync(reader, question, [], new(), CancellationToken.None);

        Assert.HasCount(1, reader.Calls);
        Assert.AreEqual("Exodus 35:3", reader.Calls[0].Reference);
    }

    [TestMethod]
    [TestCategory("Regression")]
    public async Task ReadAsync_ManyTeachingReferences_BoundsCanonicalReads()
    {
        var reader = new RecordingReader();
        var references = string.Join('\n', Enumerable.Range(1, 10).Select(number => $"[{number}] Genesis 1:{number}"));
        var question = new GroundedQuestion { Question = "Explain this teaching.", TeachingContext = new("diaspora:2026-08-29", "Creation", "A teaching about creation.", references, null) };

        await ConversationReferenceGuide.ReadAsync(reader, question, [], new(), CancellationToken.None);

        Assert.HasCount(8, reader.Calls);
        Assert.AreEqual("Genesis 1:8", reader.Calls[^1].Reference);
    }

    private sealed class RecordingReader : ICanonicalSourceReader
    {
        internal List<(string Reference, SourceRetrievalQuery Filters)> Calls { get; } = [];

        /// <inheritdoc/>
        public Task<IReadOnlyList<SourceSegment>> ReadAsync(string reference, SourceRetrievalQuery filters, CancellationToken cancellationToken = default)
        {
            Calls.Add((reference, filters));
            return Task.FromResult<IReadOnlyList<SourceSegment>>([]);
        }
    }
}
