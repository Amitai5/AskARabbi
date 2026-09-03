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
            UserProfile = new UserProfile
            {
                Name = "Test User",
                DateOfBirth = new DateOnly(2001, 12, 17),
                TimeOfBirth = new TimeOnly(9, 30),
                BirthTimeZone = "America/Los_Angeles",
                JewishHeritage = "Mizrahi",
            },
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
    public async Task AnswerAsync_BarMitzvahPortionAndSummary_ResolvesCalendarBeforeRetrievingTorahText()
    {
        // Arrange
        var vayigash = new SourceSegment
        {
            SegmentId = "sefaria:genesis:segment:00000001",
            DocumentId = "sefaria:genesis",
            CanonicalReference = "Genesis 44:18",
            DocumentOrdinal = 0,
            Text = "Then Judah approached Joseph and pleaded for Benjamin before Joseph revealed himself to his brothers.",
            Title = "Genesis",
            HebrewTitle = "בראשית",
            Language = "English",
            LanguageCode = "en",
            Collection = "Torah",
            Categories = ["Torah"],
            Version = "Test translation",
            License = "CC0",
            LicenseCategory = SourceLicenseCategory.Cc0,
            SourceUrl = "https://www.sefaria.org/Genesis.44.18",
            FilePath = "Data/NormalizedData/Sefaria/Torah/Genesis/Test.md",
        };
        var retriever = new ResolvedParashahRetriever(vayigash);
        var registry = new AIToolRegistry([new CalendarAITools(new HebrewCalendarService())]);
        var answerEngine = new CompositeCalendarAnswerEngine(vayigash.Text);
        var service = new GroundedAnswerService(retriever, answerEngine, new SupportingAuditEngine(), CreatePrompts(), new GroundedAnswerOptions { MaximumEnrichmentHits = 0 }, new FixedTimeProvider(), registry);
        var question = new GroundedQuestion
        {
            Question = "What is my Torah portion for my bar mitzvah and what is it about?",
            SourceKeys = ["collection:Torah", "collection:Talmud"],
            UserProfile = new UserProfile
            {
                Name = "Test User",
                DateOfBirth = new DateOnly(2001, 12, 17),
                TimeOfBirth = new TimeOnly(9, 30),
                BirthTimeZone = "America/Los_Angeles",
                JewishHeritage = "Mizrahi",
            },
        };

        // Act
        var result = await service.AnswerAsync(question, []);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(1, retriever.SearchCallCount);
        Assert.IsNotNull(retriever.LastQuery);
        StringAssert.Contains(retriever.LastQuery.QueryText, "Vayigash");
        StringAssert.Contains(retriever.LastQuery.QueryText, "Genesis 45:1");
        Assert.AreEqual("Genesis 44:18", retriever.LastQuery.ExactCanonicalReference);
        CollectionAssert.AreEqual(new[] { "collection:Torah" }, retriever.LastQuery.SourceKeys.ToArray());
        Assert.IsTrue(answerEngine.NonToolOverloadWasUsed);
        Assert.IsFalse(answerEngine.ToolOverloadWasUsed);
        Assert.IsNotNull(answerEngine.LastMessages);
        StringAssert.Contains(answerEngine.LastMessages[^1].Content, vayigash.Text);
        StringAssert.Contains(answerEngine.LastMessages[^1].Content, "calculated regular parashah");
        Assert.IsNotNull(result.Answer);
        CollectionAssert.AreEquivalent(new[] { "Genesis 44:18", "Calculated weekly Torah reading" }, result.Answer.Citations.Select(citation => citation.CanonicalReference).ToArray());
    }

    private static GroundedPromptSet CreatePrompts() => new()
    {
        SystemBehaviorPrompt = "Use only supplied evidence and local calendar tools.",
        PriorUserContextPrompt = $"Prior user: {GroundedPromptSet.ContextPlaceholder}",
        PriorAssistantContextPrompt = $"Prior assistant: {GroundedPromptSet.ContextPlaceholder}",
        CurrentQuestionInstruction = "Call a calendar tool and cite its exact result.",
        EvidenceStartMarker = "BEGIN_EVIDENCE",
        EvidenceEndMarker = "END_EVIDENCE",
        ValidationRepairPrompt = $"Repair: {GroundedPromptSet.ValidationErrorPlaceholder}",
        InterpretiveNotice = "One interpretation.",
        ResponseJsonSchema = "{\"type\":\"object\"}",
        SupportValidationPrompt = "Audit support.",
        SupportValidationJsonSchema = "{\"type\":\"object\"}",
    };

    private sealed class EmptyRetriever : ISourceRetriever
    {
        public Task<IReadOnlyList<SourceRetrievalHit>> SearchAsync(SourceRetrievalQuery query, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SourceRetrievalHit>>([]);

        public Task<IReadOnlyList<SourceSegment>> GetContextAsync(string documentId, int documentOrdinal, int radius, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SourceSegment>>([]);
    }

    private sealed class CalendarAnswerEngine : IAIEngine
    {
        internal bool ToolOverloadWasUsed { get; private set; }

        public Task<AIEngineResult<T>> GenerateStructuredAsync<T>(IReadOnlyList<AIMessage> messages, string schemaName, BinaryData jsonSchema, CancellationToken cancellationToken = default) => throw new AssertFailedException("The tool-aware engine overload should be used.");

        public async Task<AIEngineResult<T>> GenerateStructuredAsync<T>(IReadOnlyList<AIMessage> messages, string schemaName, BinaryData jsonSchema, AIToolExecutionSession toolSession, CancellationToken cancellationToken = default)
        {
            ToolOverloadWasUsed = true;
            var output = await toolSession.ExecuteAsync("find_parashah_for_week", BinaryData.FromString("{\"hebrewAnniversaryAge\":13}"), cancellationToken);
            using var document = JsonDocument.Parse(output);
            var evidenceId = document.RootElement.GetProperty("evidence").GetProperty("evidenceId").GetString() ?? throw new AssertFailedException("Tool evidence ID was missing.");
            var exactText = document.RootElement.GetProperty("evidence").GetProperty("exactText").GetString() ?? throw new AssertFailedException("Tool evidence text was missing.");
            var draft = new GroundedAnswerDraft
            {
                Claims =
                [
                    new GroundedClaimDraft
                    {
                        Text = "The calculated weekly portion is Vayigash under the stated assumptions.",
                        EvidenceIds = [evidenceId],
                        Quotations = [new GroundedQuotationDraft { EvidenceId = evidenceId, Text = exactText, Role = "Provides the deterministic calendar result and its assumptions." }],
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
        private readonly string torahQuotation;

        internal CompositeCalendarAnswerEngine(string torahQuotation)
        {
            this.torahQuotation = torahQuotation;
        }

        internal bool NonToolOverloadWasUsed { get; private set; }

        internal bool ToolOverloadWasUsed { get; private set; }

        internal IReadOnlyList<AIMessage>? LastMessages { get; private set; }

        public Task<AIEngineResult<T>> GenerateStructuredAsync<T>(IReadOnlyList<AIMessage> messages, string schemaName, BinaryData jsonSchema, CancellationToken cancellationToken = default)
        {
            NonToolOverloadWasUsed = true;
            LastMessages = messages.ToArray();
            using var payload = JsonDocument.Parse(messages[^1].Content);
            var evidence = payload.RootElement.GetProperty("evidenceBoundary").GetProperty("items").EnumerateArray().ToArray();
            var torahEvidence = evidence.Single(item => item.GetProperty("canonicalReference").GetString() == "Genesis 44:18");
            var calendarEvidence = evidence.Single(item => item.GetProperty("canonicalReference").GetString() == "Calculated weekly Torah reading");
            var torahEvidenceId = torahEvidence.GetProperty("evidenceId").GetString() ?? throw new AssertFailedException("Torah evidence ID was missing.");
            var calendarEvidenceId = calendarEvidence.GetProperty("evidenceId").GetString() ?? throw new AssertFailedException("Calendar evidence ID was missing.");
            var calendarQuotation = calendarEvidence.GetProperty("text").GetString() ?? throw new AssertFailedException("Calendar evidence text was missing.");
            var draft = new GroundedAnswerDraft
            {
                Claims =
                [
                    new GroundedClaimDraft
                    {
                        Text = "The calendar calculation identifies Vayigash, and its opening describes Judah pleading with Joseph for Benjamin before Joseph reveals himself.",
                        EvidenceIds = [calendarEvidenceId, torahEvidenceId],
                        Quotations =
                        [
                            new GroundedQuotationDraft { EvidenceId = calendarEvidenceId, Text = calendarQuotation, Role = "Identifies the calculated bar mitzvah portion and assumptions." },
                            new GroundedQuotationDraft { EvidenceId = torahEvidenceId, Text = torahQuotation, Role = "Provides Torah context explaining what the portion is about." },
                        ],
                    },
                ],
                Disagreements = [],
                Limitations = [],
                HumanGuidanceRecommended = false,
            };
            return Task.FromResult(AIEngineResult<T>.Success((T)(object)draft, new AIResponseDiagnostics("answer", "test", new AIUsage(1, 1, 2), TimeSpan.FromMilliseconds(1), 1)));
        }

        public Task<AIEngineResult<T>> GenerateStructuredAsync<T>(IReadOnlyList<AIMessage> messages, string schemaName, BinaryData jsonSchema, AIToolExecutionSession toolSession, CancellationToken cancellationToken = default)
        {
            ToolOverloadWasUsed = true;
            throw new AssertFailedException("The resolved composite calendar question should not require a model-driven tool round trip.");
        }
    }

    private sealed class ResolvedParashahRetriever : ISourceRetriever
    {
        private readonly SourceSegment segment;

        internal ResolvedParashahRetriever(SourceSegment segment)
        {
            this.segment = segment;
        }

        internal int SearchCallCount { get; private set; }

        internal SourceRetrievalQuery? LastQuery { get; private set; }

        public Task<IReadOnlyList<SourceRetrievalHit>> SearchAsync(SourceRetrievalQuery query, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SearchCallCount++;
            LastQuery = query;
            var hits = string.Equals(query.ExactCanonicalReference, segment.CanonicalReference, StringComparison.OrdinalIgnoreCase)
                ? new[] { new SourceRetrievalHit(segment, 1, false) }
                : [];
            return Task.FromResult<IReadOnlyList<SourceRetrievalHit>>(hits);
        }

        public Task<IReadOnlyList<SourceSegment>> GetContextAsync(string documentId, int documentOrdinal, int radius, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SourceSegment>>([]);
    }

    private sealed class SupportingAuditEngine : IAIEngine
    {
        public Task<AIEngineResult<T>> GenerateStructuredAsync<T>(IReadOnlyList<AIMessage> messages, string schemaName, BinaryData jsonSchema, CancellationToken cancellationToken = default)
        {
            var draft = new GroundedSupportValidationDraft
            {
                IsResponsive = true,
                OverallExplanation = "The draft directly answers the requested calendar question.",
                Evaluations = [new GroundedSupportEvaluationDraft { StatementId = "C1", IsRelevant = true, IsSupported = true, Explanation = "The calculation directly supports the claim." }],
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
