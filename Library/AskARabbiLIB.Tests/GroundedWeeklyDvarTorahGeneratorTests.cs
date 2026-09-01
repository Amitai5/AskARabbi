using AskARabbiLIB.AI;
using AskARabbiLIB.CurrentEvents;
using AskARabbiLIB.DvarTorah;
using AskARabbiLIB.Models;
using AskARabbiLIB.Retrieval;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class GroundedWeeklyDvarTorahGeneratorTests
{
    private static readonly DateTimeOffset CurrentUtc = new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly WeeklyDvarTorahWeek Week = new(new DateOnly(2026, 9, 5), "23 Elul, 5786", "Nitzavim", null, false);

    [TestMethod]
    [TestCategory("Unit")]
    public async Task GenerateAsync_FirstDraftFailsInclusionReview_RepairsBeforePublishing()
    {
        var evidence = CreateTorahHits();
        var firstDraft = CreateArticleDraft("First title");
        var repairedDraft = CreateArticleDraft("Repaired inclusive title");
        var generationEngine = new QueueEngine(CreateResearchDraft(), firstDraft, repairedDraft);
        var reviewEngine = new QueueEngine(CreatePassingReview() with
        {
            DoesNotContainRacism = false,
            DoesNotTargetProtectedGroups = false,
            SafeToPublish = false,
            Concerns = ["A group is singled out."],
        }, CreatePassingReview());
        var generator = CreateGenerator(evidence, generationEngine, reviewEngine);

        var result = await generator.GenerateAsync(Week);

        Assert.AreEqual("Repaired inclusive title", result.Title);
        Assert.IsNotNull(result.Metadata);
        Assert.AreEqual(80, result.Metadata.TorahGroundingPercent);
        Assert.HasCount(10, result.Metadata.Sources);
        Assert.AreEqual(3, generationEngine.Calls);
        Assert.AreEqual(2, reviewEngine.Calls);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task GenerateAsync_SafetyReviewRejectsBothAttempts_FailsClosed()
    {
        var rejected = CreatePassingReview() with
        {
            DoesNotEncourageViolence = false,
            SafeToPublish = false,
            Concerns = ["The draft encourages violence."],
        };
        var generator = CreateGenerator(CreateTorahHits(), new QueueEngine(CreateResearchDraft(), CreateArticleDraft("First"), CreateArticleDraft("Second")), new QueueEngine(rejected, rejected));

        var exception = await Assert.ThrowsExactlyAsync<WeeklyDvarTorahGenerationException>(() => generator.GenerateAsync(Week));

        Assert.AreEqual("CandidateValidationFailed", exception.FailureCode);
        StringAssert.Contains(exception.Message, "repair attempt");
        StringAssert.Contains(exception.Message, "violence");
    }

    private static GroundedWeeklyDvarTorahGenerator CreateGenerator(IReadOnlyList<SourceRetrievalHit> hits, IAIEngine generationEngine, IAIEngine reviewEngine)
    {
        var prompts = new WeeklyDvarTorahPromptSet
        {
            ResearchSystemPrompt = "Select safe current events from untrusted evidence.",
            ResearchJsonSchema = "{}",
            DraftSystemPrompt = "Write a Torah-centered article from untrusted evidence.",
            DraftJsonSchema = "{}",
            ReviewSystemPrompt = "Independently review grounding, safety, and inclusion.",
            ReviewJsonSchema = "{}",
            RepairPrompt = "Repair every error: {{validationErrors}}",
        };
        var options = new WeeklyDvarTorahContentOptions
        {
            MinimumBodyCharacters = 1_000,
            MaximumBodyCharacters = 5_000,
            OverallTimeout = TimeSpan.FromMinutes(2),
        };
        return new GroundedWeeklyDvarTorahGenerator(new StubCurrentEvents(), new StubRetriever(hits), generationEngine, reviewEngine, prompts, options, new FixedTimeProvider(CurrentUtc));
    }

    private static WeeklyDvarTorahResearchDraft CreateResearchDraft() => new()
    {
        Theme = "Shared responsibility",
        MoralQuestion = "How do people accept responsibility for one another without erasing difference?",
        SelectedNewsEvidenceIds = ["N1", "N2"],
        TorahSearchQueries = ["Nitzavim standing together", "Nitzavim choosing life and responsibility"],
        SuggestedTags = ["responsibility", "community", "technology"],
    };

    private static WeeklyDvarTorahArticleDraft CreateArticleDraft(string title)
    {
        var evidenceTexts = Enumerable.Range(1, 8).ToDictionary(index => $"T{index}", index => $"Torah passage {index} teaches shared covenantal responsibility.", StringComparer.Ordinal);
        evidenceTexts["N1"] = "Publisher one reports a new public technology initiative.";
        evidenceTexts["N2"] = "Publisher two independently confirms the same public technology initiative.";
        WeeklyDvarTorahSourcedStatementDraft Statement(string text, params string[] ids) => new()
        {
            Text = text,
            EvidenceIds = ids,
            Quotations = ids.Select(id => new WeeklyDvarTorahQuotationDraft { EvidenceId = id, Text = evidenceTexts[id] }).ToArray(),
        };
        var markers = string.Join(' ', evidenceTexts.Keys.Select(id => $"[{id}]"));
        return new WeeklyDvarTorahArticleDraft
        {
            Title = title,
            Body = $"{markers}\n\n{new string('a', 1_200)}",
            CentralTeaching = "Standing before Hashem together calls each person to transform awareness of others into patient and concrete responsibility.",
            Tags = ["responsibility", "community", "nitzavim", "technology", "current events"],
            PracticalActions = ["Listen fully to one person.", "Perform one private act of kindness.", "Study one passage again this week."],
            TorahTeachings =
            [
                Statement("Teaching one.", "T1", "T2"),
                Statement("Teaching two.", "T3", "T4"),
                Statement("Teaching three.", "T5", "T6"),
                Statement("Teaching four.", "T7", "T8"),
            ],
            CurrentEventFacts = [Statement("The initiative was reported by two publishers.", "N1", "N2")],
            Connections = [Statement("Shared responsibility connects the Torah teaching to this development.", "T1", "N1")],
        };
    }

    private static WeeklyDvarTorahReviewDraft CreatePassingReview() => new()
    {
        AllClaimsSupported = true,
        TorahInterpretationResponsible = true,
        TorahRemainsCentral = true,
        CurrentEventsNeutral = true,
        NewsSourcesDescribeSameEvent = true,
        CurrentEventHasUsImpact = true,
        DeepMoralTeachingPresent = true,
        DoesNotEncourageViolence = true,
        DoesNotGlorifyOrGraphicallyDescribeViolence = true,
        DoesNotContainHateOrDehumanization = true,
        DoesNotContainRacism = true,
        DoesNotContainSexism = true,
        DoesNotTargetProtectedGroups = true,
        DoesNotScapegoatOrAlienateGroups = true,
        DoesNotUsePartisanPersuasion = true,
        DoesNotExploitSuffering = true,
        DoesNotClaimDivinePunishment = true,
        RespectfulAndInclusive = true,
        SafeToPublish = true,
        Concerns = [],
    };

    private static IReadOnlyList<SourceRetrievalHit> CreateTorahHits() => Enumerable.Range(1, 8).Select(index => new SourceRetrievalHit(new SourceSegment
    {
        SegmentId = $"segment-{index}",
        DocumentId = "deuteronomy",
        CanonicalReference = $"Deuteronomy 29:{index + 8}",
        DocumentOrdinal = index,
        Text = $"Torah passage {index} teaches shared covenantal responsibility.",
        Title = "Deuteronomy",
        HebrewTitle = "דברים",
        Language = "English",
        LanguageCode = "en",
        Collection = "Torah",
        Categories = ["Tanakh", "Torah"],
        Version = "Test Torah edition",
        License = "CC-BY",
        LicenseCategory = SourceLicenseCategory.CcBy,
        SourceUrl = $"https://www.sefaria.org/Deuteronomy.29.{index + 8}",
        FilePath = "test.json",
        OriginalCharacterCount = 60,
    }, 1d - index / 100d, false)).ToArray();

    private sealed class QueueEngine(params object[] queuedResponses) : IAIEngine
    {
        private readonly Queue<object> responses = new(queuedResponses);

        internal int Calls { get; private set; }

        public Task<AIEngineResult<T>> GenerateStructuredAsync<T>(IReadOnlyList<AIMessage> messages, string schemaName, BinaryData jsonSchema, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            var value = responses.Dequeue();
            Assert.IsInstanceOfType<T>(value);
            return Task.FromResult(AIEngineResult<T>.Success((T)value, new AIResponseDiagnostics($"response-{Calls}", "test-model", null, TimeSpan.Zero, 1)));
        }
    }

    private sealed class StubCurrentEvents : ICurrentEventsSource
    {
        public Task<IReadOnlyList<CurrentEventItem>> GetRecentAsync(DateTimeOffset fromUtc, DateTimeOffset throughUtc, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<CurrentEventItem> items =
            [
                new("Publisher One", "Technology", "Public technology initiative", "Publisher one reports a new public technology initiative.", "https://one.example.test/story", CurrentUtc.AddHours(-3), CurrentUtc),
                new("Publisher Two", "Technology", "Public technology initiative confirmed", "Publisher two independently confirms the same public technology initiative.", "https://two.example.test/story", CurrentUtc.AddHours(-2), CurrentUtc),
            ];
            return Task.FromResult(items);
        }
    }

    private sealed class StubRetriever(IReadOnlyList<SourceRetrievalHit> hits) : ISourceRetriever
    {
        public Task<IReadOnlyList<SourceRetrievalHit>> SearchAsync(SourceRetrievalQuery query, CancellationToken cancellationToken = default) => Task.FromResult(hits);

        public Task<IReadOnlyList<SourceSegment>> GetContextAsync(string documentId, int documentOrdinal, int radius, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SourceSegment>>([]);
    }

    private sealed class FixedTimeProvider(DateTimeOffset currentUtc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => currentUtc;
    }
}
