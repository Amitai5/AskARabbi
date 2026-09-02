using System.Text.Json;
using AskARabbiLIB.AI;
using AskARabbiLIB.AI.Tools;
using AskARabbiLIB.Calendar;
using AskARabbiLIB.Grounding;
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
