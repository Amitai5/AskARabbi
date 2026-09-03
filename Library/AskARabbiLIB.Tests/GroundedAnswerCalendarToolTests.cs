using System.Collections.Concurrent;
using System.Text.Json;
using AskARabbiLIB.AI;
using AskARabbiLIB.AI.Tools;
using AskARabbiLIB.Calendar;
using AskARabbiLIB.Grounding;
using AskARabbiLIB.Models;
using AskARabbiLIB.Profiles;
using AskARabbiLIB.Retrieval;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class GroundedAnswerCalendarToolTests
{
    [TestMethod]
    [TestCategory("Regression")]
    public async Task AnswerAsync_ToolOnlyQuestionWithNoCorpusHits_CallsCalendarToolAndValidatesCalculatedEvidence()
    {
        // Arrange
        var registry = new AIToolRegistry([new CalendarAITools(new HebrewCalendarService())]);
        var answerEngine = new CalendarAnswerEngine();
        var service = new GroundedAnswerService(new EmptyRetriever(), answerEngine, new SupportingAuditEngine(), CreatePrompts(), new GroundedAnswerOptions { MaximumEnrichmentHits = 0 }, new FixedTimeProvider(), registry);
        var question = new GroundedQuestion
        {
            Question = "What was my bar mitzvah parashah?",
            UserProfile = CreateProfile(),
        };

        // Act
        var result = await service.AnswerAsync(question, []);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.IsTrue(answerEngine.ToolOverloadWasUsed);
        Assert.IsNotNull(result.Answer);
        Assert.AreEqual("Calendar calculations", result.Answer.Citations[0].Collection);
        StringAssert.Contains(result.Answer.Claims[0].DirectQuotation, "Vayigash");
        Assert.IsNotNull(result.Evidence);
        Assert.HasCount(1, result.Evidence.Items);
    }

    [TestMethod]
    [TestCategory("Regression")]
    public async Task AnswerAsync_BarMitzvahPortionAndSummary_RetrievesWholeStoryAndProducesRequiredConversationShape()
    {
        // Arrange
        var passages = CreateVayigashPassages();
        var retriever = new ResolvedParashahRetriever(passages);
        var registry = new AIToolRegistry([new CalendarAITools(new HebrewCalendarService())]);
        var answerEngine = new CompositeCalendarAnswerEngine(passages);
        var service = new GroundedAnswerService(retriever, answerEngine, new SupportingAuditEngine(), CreatePrompts(), new GroundedAnswerOptions { MaximumEvidenceSegments = 10, MaximumSegmentsPerDocument = 3, MaximumEnrichmentHits = 0 }, new FixedTimeProvider(), registry);
        var question = new GroundedQuestion
        {
            Question = "What is my Torah portion for my bar mitzvah and what is it about?",
            SourceKeys = ["collection:Torah", "collection:Talmud"],
            ConversationLanguage = "English",
            QuotationLanguage = "English",
            UserProfile = CreateProfile(),
        };

        // Act
        var result = await service.AnswerAsync(question, []);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(6, retriever.SearchCallCount);
        CollectionAssert.AreEquivalent(
            new[] { "Genesis 44:18", "Genesis 45:1", "Genesis 46:1", "Genesis 47:1", "Genesis 47:27" },
            retriever.Queries.Where(query => query.ExactCanonicalReference is not null).Select(query => query.ExactCanonicalReference).ToArray());
        Assert.IsTrue(retriever.Queries.Any(query => query.QueryText?.Contains("Vayigash", StringComparison.Ordinal) == true));
        Assert.IsTrue(retriever.Queries.All(query => query.SourceKeys.SequenceEqual(["collection:Torah"])), "Every compound search must stay inside Torah.");
        Assert.AreEqual(0, retriever.ContextCallCount);
        Assert.IsTrue(answerEngine.NonToolOverloadWasUsed);
        Assert.IsFalse(answerEngine.ToolOverloadWasUsed);
        Assert.IsNotNull(answerEngine.LastMessages);
        using var promptPayload = JsonDocument.Parse(answerEngine.LastMessages[^1].Content);
        var promptEvidenceTexts = promptPayload.RootElement
            .GetProperty("evidenceBoundary")
            .GetProperty("items")
            .EnumerateArray()
            .Select(item => item.GetProperty("text").GetString())
            .ToArray();
        foreach (var passage in passages)
        {
            Assert.IsTrue(promptEvidenceTexts.Contains(passage.Text, StringComparer.Ordinal), $"Prompt evidence did not contain {passage.CanonicalReference}.");
        }
        StringAssert.Contains(answerEngine.LastMessages[^1].Content, "The short answer is: the parashah for the Shabbat on or after your 13th Hebrew birthday is Vayigash.");
        StringAssert.Contains(answerEngine.LastMessages[^1].Content, "5 Tevet, 5775");
        Assert.IsNotNull(result.Answer);
        Assert.HasCount(3, result.Answer.Claims);
        Assert.HasCount(0, result.Answer.Limitations);
        var rendered = new GroundedAnswerTextRenderer().Render(result.Answer);
        StringAssert.StartsWith(rendered, "The short answer is: the parashah for the Shabbat on or after your 13th Hebrew birthday is Vayigash.");
        Assert.HasCount(3, rendered.Split("\n\n", StringSplitOptions.None));
        Assert.IsFalse(rendered.Contains("tool", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(rendered.Contains("What these sources do not fully answer", StringComparison.Ordinal));
        Assert.IsFalse(rendered.Contains("personal halakhic rulings", StringComparison.Ordinal));
        CollectionAssert.AreEquivalent(
            new[] { "Genesis 44:18", "Genesis 45:1", "Genesis 46:1", "Genesis 47:1", "Genesis 47:27", "Weekly Torah reading" },
            result.Answer.Citations.Select(citation => citation.CanonicalReference).ToArray());
    }

    [TestMethod]
    [TestCategory("Regression")]
    public async Task AnswerAsync_BarMitzvahPortionAndSummary_WithSparseTorahCoverage_FailsBeforeCallingModel()
    {
        // Arrange
        var passages = CreateVayigashPassages().Take(2).ToArray();
        var retriever = new ResolvedParashahRetriever(passages);
        var registry = new AIToolRegistry([new CalendarAITools(new HebrewCalendarService())]);
        var answerEngine = new CompositeCalendarAnswerEngine(passages);
        var service = new GroundedAnswerService(retriever, answerEngine, new SupportingAuditEngine(), CreatePrompts(), new GroundedAnswerOptions { MaximumEnrichmentHits = 0 }, new FixedTimeProvider(), registry);
        var question = new GroundedQuestion
        {
            Question = "What is my Torah portion for my bar mitzvah and what is it about?",
            SourceKeys = ["collection:Torah"],
            UserProfile = CreateProfile(),
        };

        // Act
        var result = await service.AnswerAsync(question, []);

        // Assert
        Assert.AreEqual(GroundedAnswerStatus.InsufficientEvidence, result.Status);
        Assert.IsFalse(answerEngine.NonToolOverloadWasUsed);
        StringAssert.Contains(result.ErrorMessage, "could not load enough of this Torah portion");
    }

    private static GroundedPromptSet CreatePrompts() => new()
    {
        SystemBehaviorPrompt = "Use only supplied evidence. Never expose internal implementation mechanisms in an answer.",
        PriorUserContextPrompt = $"Prior user: {GroundedPromptSet.ContextPlaceholder}",
        PriorAssistantContextPrompt = $"Prior assistant: {GroundedPromptSet.ContextPlaceholder}",
        CurrentQuestionInstruction = "Use calendar capabilities privately when needed and cite exact results without naming the mechanism.",
        EvidenceStartMarker = "BEGIN_EVIDENCE",
        EvidenceEndMarker = "END_EVIDENCE",
        ValidationRepairPrompt = $"Repair: {GroundedPromptSet.ValidationErrorPlaceholder}",
        InterpretiveNotice = "One interpretation.",
        ResponseJsonSchema = "{\"type\":\"object\"}",
        SupportValidationPrompt = "Audit support.",
        SupportValidationJsonSchema = "{\"type\":\"object\"}",
    };

    private static UserProfile CreateProfile() => new()
    {
        Name = "Test User",
        DateOfBirth = new DateOnly(2001, 12, 17),
        TimeOfBirth = new TimeOnly(9, 30),
        BirthTimeZone = "America/Los_Angeles",
        JewishHeritage = "Mizrahi",
    };

    private static IReadOnlyList<SourceSegment> CreateVayigashPassages() =>
    [
        CreatePassage("Genesis 44:18", 0, "Then Judah came near unto him, and said: 'Oh, my lord, let thy servant, I pray thee, speak a word in my lord's ears.'"),
        CreatePassage("Genesis 45:1", 1, "Then Joseph could not refrain himself before all them that stood by him; and he cried: 'Cause every man to go out from me.' And there stood no man with him, while Joseph made himself known unto his brethren."),
        CreatePassage("Genesis 46:1", 2, "And Israel took his journey with all that he had, and came to Beer-sheba, and offered sacrifices unto the God of his father Isaac."),
        CreatePassage("Genesis 47:1", 3, "Then Joseph went in and told Pharaoh, and said: 'My father and my brethren are come out of the land of Canaan; and, behold, they are in the land of Goshen.'"),
        CreatePassage("Genesis 47:27", 4, "And Israel dwelt in the land of Egypt, in the land of Goshen; and they got them possessions therein, and were fruitful, and multiplied exceedingly."),
    ];

    private static SourceSegment CreatePassage(string canonicalReference, int ordinal, string text) => new()
    {
        SegmentId = $"sefaria:genesis:segment:{ordinal:D8}",
        DocumentId = "sefaria:genesis",
        CanonicalReference = canonicalReference,
        DocumentOrdinal = ordinal,
        Text = text,
        Title = "Genesis",
        HebrewTitle = "בראשית",
        Language = "English",
        LanguageCode = "en",
        Collection = "Torah",
        Categories = ["Torah"],
        Version = "Test translation",
        License = "CC0",
        LicenseCategory = SourceLicenseCategory.Cc0,
        SourceUrl = $"https://www.sefaria.org/{canonicalReference.Replace(' ', '.').Replace(':', '.')}",
        FilePath = "Data/NormalizedData/Sefaria/Torah/Genesis/Test.md",
    };

    private sealed class EmptyRetriever : ISourceRetriever
    {
        public Task<IReadOnlyList<SourceRetrievalHit>> SearchAsync(SourceRetrievalQuery query, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SourceRetrievalHit>>([]);

        public Task<IReadOnlyList<SourceSegment>> GetContextAsync(string documentId, int documentOrdinal, int radius, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SourceSegment>>([]);
    }

    private sealed class CalendarAnswerEngine : IAIEngine
    {
        internal bool ToolOverloadWasUsed { get; private set; }

        public Task<AIEngineResult<T>> GenerateStructuredAsync<T>(IReadOnlyList<AIMessage> messages, string schemaName, BinaryData jsonSchema, CancellationToken cancellationToken = default) => throw new AssertFailedException("The function-enabled engine overload should be used.");

        public async Task<AIEngineResult<T>> GenerateStructuredAsync<T>(IReadOnlyList<AIMessage> messages, string schemaName, BinaryData jsonSchema, AIToolExecutionSession toolSession, CancellationToken cancellationToken = default)
        {
            ToolOverloadWasUsed = true;
            var output = await toolSession.ExecuteAsync("find_parashah_for_week", BinaryData.FromString("{\"hebrewAnniversaryAge\":13}"), cancellationToken);
            using var document = JsonDocument.Parse(output);
            var evidenceId = document.RootElement.GetProperty("evidence").GetProperty("evidenceId").GetString() ?? throw new AssertFailedException("Calendar evidence ID was missing.");
            var exactText = document.RootElement.GetProperty("evidence").GetProperty("exactText").GetString() ?? throw new AssertFailedException("Calendar evidence text was missing.");
            var draft = new GroundedAnswerDraft
            {
                Claims =
                [
                    new GroundedClaimDraft
                    {
                        Text = "The weekly portion is Vayigash under the stated date assumptions.",
                        EvidenceIds = [evidenceId],
                        Quotations = [new GroundedQuotationDraft { EvidenceId = evidenceId, Text = exactText, Role = "Provides the weekly-reading result and its assumptions." }],
                    },
                ],
                Disagreements = [],
                Limitations = [],
                HumanGuidanceRecommended = false,
            };
            return AIEngineResult<T>.Success((T)(object)draft, new AIResponseDiagnostics("answer", "test", new AIUsage(1, 1, 2), TimeSpan.FromMilliseconds(1), 1));
        }
    }

    private sealed class CompositeCalendarAnswerEngine : IAIEngine
    {
        private readonly IReadOnlyDictionary<string, SourceSegment> passages;

        internal CompositeCalendarAnswerEngine(IEnumerable<SourceSegment> passages)
        {
            this.passages = passages.ToDictionary(passage => passage.CanonicalReference, StringComparer.OrdinalIgnoreCase);
        }

        internal bool NonToolOverloadWasUsed { get; private set; }

        internal bool ToolOverloadWasUsed { get; private set; }

        internal IReadOnlyList<AIMessage>? LastMessages { get; private set; }

        public Task<AIEngineResult<T>> GenerateStructuredAsync<T>(IReadOnlyList<AIMessage> messages, string schemaName, BinaryData jsonSchema, CancellationToken cancellationToken = default)
        {
            NonToolOverloadWasUsed = true;
            LastMessages = messages.ToArray();
            using var payload = JsonDocument.Parse(messages[^1].Content);
            var evidence = payload.RootElement.GetProperty("evidenceBoundary").GetProperty("items").EnumerateArray().ToDictionary(item => item.GetProperty("canonicalReference").GetString() ?? string.Empty, StringComparer.OrdinalIgnoreCase);
            var calendarEvidence = evidence["Weekly Torah reading"];
            var calendarEvidenceId = GetEvidenceId(calendarEvidence);
            var calendarQuotation = calendarEvidence.GetProperty("text").GetString() ?? throw new AssertFailedException("Calendar evidence text was missing.");
            var draft = new GroundedAnswerDraft
            {
                Claims =
                [
                    new GroundedClaimDraft
                    {
                        Text = "The short answer is: the parashah for the Shabbat on or after your 13th Hebrew birthday is Vayigash.",
                        EvidenceIds = [calendarEvidenceId],
                        Quotations = [new GroundedQuotationDraft { EvidenceId = calendarEvidenceId, Text = calendarQuotation, Role = "Establishes which weekly reading falls on the relevant Shabbat." }],
                    },
                    CreateStoryClaim(
                        "Vayigash opens with Judah approaching Joseph to plead for Benjamin; Joseph is overcome and reveals his identity to his brothers.",
                        evidence,
                        "Genesis 44:18",
                        "Genesis 45:1"),
                    CreateStoryClaim(
                        "The story then follows Israel's journey toward Egypt, Joseph's presentation of the family to Pharaoh, and their settlement and growth in Goshen.",
                        evidence,
                        "Genesis 46:1",
                        "Genesis 47:1",
                        "Genesis 47:27"),
                ],
                Disagreements = [],
                Limitations = [],
                ClarifyingQuestion = null,
                HumanGuidanceRecommended = false,
            };
            return Task.FromResult(AIEngineResult<T>.Success((T)(object)draft, new AIResponseDiagnostics("answer", "test", new AIUsage(1, 1, 2), TimeSpan.FromMilliseconds(1), 1)));
        }

        public Task<AIEngineResult<T>> GenerateStructuredAsync<T>(IReadOnlyList<AIMessage> messages, string schemaName, BinaryData jsonSchema, AIToolExecutionSession toolSession, CancellationToken cancellationToken = default)
        {
            ToolOverloadWasUsed = true;
            throw new AssertFailedException("The resolved compound question should not require another function round trip.");
        }

        private GroundedClaimDraft CreateStoryClaim(string text, IReadOnlyDictionary<string, JsonElement> evidence, params string[] references)
        {
            return new GroundedClaimDraft
            {
                Text = text,
                EvidenceIds = references.Select(reference => GetEvidenceId(evidence[reference])).ToArray(),
                Quotations = references.Select(reference => new GroundedQuotationDraft
                {
                    EvidenceId = GetEvidenceId(evidence[reference]),
                    Text = passages[reference].Text,
                    Role = "Supports this stage of the Torah narrative.",
                }).ToArray(),
            };
        }

        private static string GetEvidenceId(JsonElement evidence) => evidence.GetProperty("evidenceId").GetString() ?? throw new AssertFailedException("Evidence ID was missing.");
    }

    private sealed class ResolvedParashahRetriever : ISourceRetriever
    {
        private readonly IReadOnlyDictionary<string, SourceSegment> passages;
        private int contextCallCount;
        private int searchCallCount;

        internal ResolvedParashahRetriever(IEnumerable<SourceSegment> passages)
        {
            this.passages = passages.ToDictionary(passage => passage.CanonicalReference, StringComparer.OrdinalIgnoreCase);
        }

        internal int SearchCallCount => searchCallCount;

        internal int ContextCallCount => contextCallCount;

        internal ConcurrentBag<SourceRetrievalQuery> Queries { get; } = [];

        public Task<IReadOnlyList<SourceRetrievalHit>> SearchAsync(SourceRetrievalQuery query, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref searchCallCount);
            Queries.Add(query);
            if (query.ExactCanonicalReference is not null && passages.TryGetValue(query.ExactCanonicalReference, out var exact))
            {
                return Task.FromResult<IReadOnlyList<SourceRetrievalHit>>([new SourceRetrievalHit(exact, 1, true)]);
            }

            var semantic = string.IsNullOrWhiteSpace(query.ExactCanonicalReference)
                ? passages.Values.Reverse().Select((passage, index) => new SourceRetrievalHit(passage, 1 - index * 0.01, false)).ToArray()
                : [];
            return Task.FromResult<IReadOnlyList<SourceRetrievalHit>>(semantic);
        }

        public Task<IReadOnlyList<SourceSegment>> GetContextAsync(string documentId, int documentOrdinal, int radius, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref contextCallCount);
            return Task.FromResult<IReadOnlyList<SourceSegment>>([]);
        }
    }

    private sealed class SupportingAuditEngine : IAIEngine
    {
        public Task<AIEngineResult<T>> GenerateStructuredAsync<T>(IReadOnlyList<AIMessage> messages, string schemaName, BinaryData jsonSchema, CancellationToken cancellationToken = default)
        {
            using var payload = JsonDocument.Parse(messages[^1].Content);
            var evaluations = payload.RootElement.GetProperty("statements").EnumerateArray().Select(statement => new GroundedSupportEvaluationDraft
            {
                StatementId = statement.GetProperty("statementId").GetString() ?? throw new AssertFailedException("Statement ID was missing."),
                IsRelevant = true,
                IsSupported = true,
                Explanation = "The cited passage directly supports the statement.",
            }).ToArray();
            var draft = new GroundedSupportValidationDraft
            {
                IsResponsive = true,
                OverallExplanation = "The draft directly answers the requested calendar and Torah-content question.",
                Evaluations = evaluations,
            };
            return Task.FromResult(AIEngineResult<T>.Success((T)(object)draft, new AIResponseDiagnostics("audit", "test", new AIUsage(1, 1, 2), TimeSpan.FromMilliseconds(1), 1)));
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public override DateTimeOffset GetUtcNow() => new(2026, 8, 31, 18, 0, 0, TimeSpan.Zero);
    }
}
