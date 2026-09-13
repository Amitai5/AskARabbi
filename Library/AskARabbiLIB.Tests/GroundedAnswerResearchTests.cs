using System.Text.Json;
using AskARabbiLIB.AI;
using AskARabbiLIB.AI.Tools;
using AskARabbiLIB.Grounding;
using AskARabbiLIB.Retrieval;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class GroundedAnswerResearchTests
{
    [TestMethod]
    public async Task AnswerAsync_SemanticMiss_UsesVerifiedKeywordEvidenceBeforeDrafting()
    {
        var sources = new SourceResearchTestData.Sources { InitialMiss = true };
        var keywords = new SourceResearchTestData.Sources();
        var retriever = new ResearchSourceRetriever(sources, keywords);
        var registry = new AIToolRegistry([new SourceResearchAITools(retriever, sources)]);
        var engine = new ResearchEngine(false, useInitialEvidence: true);
        var service = new GroundedAnswerService(retriever, engine, Prompts(), new Audit(), new GroundedAnswerOptions { MaximumEnrichmentHits = 0 }, new FixedTime(), registry);

        var result = await service.AnswerAsync(new GroundedQuestion { Question = SourceResearchTestData.Question }, []);

        Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
        Assert.HasCount(1, sources.Searches);
        Assert.HasCount(0, sources.Reads);
        Assert.HasCount(1, keywords.Searches);
        Assert.IsNotNull(result.Answer);
        Assert.AreEqual(SourceResearchTestData.Reference, result.Answer.Citations[0].CanonicalReference);
        Assert.IsNull(engine.InitialRequiredToolName);
    }

    [TestMethod]
    public async Task AnswerAsync_KeywordSourceUnavailable_DoesNotInventEvidence()
    {
        var sources = new SourceResearchTestData.Sources { Empty = true };
        var registry = new AIToolRegistry([new SourceResearchAITools(sources, sources)]);
        var service = new GroundedAnswerService(sources, new ResearchEngine(false, useInitialEvidence: true), Prompts(), new Audit(), new GroundedAnswerOptions { MaximumEnrichmentHits = 0 }, new FixedTime(), registry);

        var result = await service.AnswerAsync(new GroundedQuestion { Question = SourceResearchTestData.Question }, []);

        Assert.AreEqual(GroundedAnswerStatus.ValidationFailed, result.Status);
        Assert.IsNull(result.Answer);
        Assert.AreEqual(0, result.Trace.EvidenceCount);
    }

    [TestMethod]
    public void ProductionPrompts_MissingContext_AllowResearchDuringDraftAndRepair()
    {
        var prompts = Prompts();

        StringAssert.Contains(prompts.SystemBehaviorPrompt, "religious passage FIRST");
        StringAssert.Contains(prompts.SystemBehaviorPrompt, "first research call");
        StringAssert.Contains(prompts.ValidationRepairPrompt, "use remaining source-research calls");
        StringAssert.Contains(prompts.ValidationRepairPrompt, "newly returned by successful research");
        Assert.IsFalse(prompts.ValidationRepairPrompt.Contains("exactly the same evidence packet", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task AnswerAsync_SquashQuestion_ResearchesMissingSourcesInsteadOfDisplayingDeflection(bool firstDraftDeflects)
    {
        var sources = new SourceResearchTestData.Sources { InitialMiss = true };
        var registry = new AIToolRegistry([new SourceResearchAITools(sources, sources)]);
        var engine = new ResearchEngine(firstDraftDeflects);
        var audit = new Audit();
        var service = new GroundedAnswerService(sources, engine, Prompts(), audit, new GroundedAnswerOptions { MaximumEnrichmentHits = 0 }, new FixedTime(), registry);

        var result = await service.AnswerAsync(new GroundedQuestion { Question = SourceResearchTestData.Question, QuotationLanguage = "English" }, []);

        Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
        Assert.IsNotNull(result.Answer);
        Assert.AreEqual(SourceResearchTestData.Reference, result.Answer.Citations[0].CanonicalReference);
        StringAssert.Contains(result.Answer.Claims[0].Text, "wordplay");
        Assert.IsFalse(result.Answer.Claims[0].Text.Contains("supplied", StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual(firstDraftDeflects ? GroundedValidationStatus.Repaired : GroundedValidationStatus.Passed, result.Trace.ValidationStatus);
        Assert.HasCount(2, sources.Searches);
        Assert.HasCount(1, sources.Reads);
        Assert.IsTrue(audit.SawReligiousEvidence);
        Assert.AreEqual("search_source_passages", engine.InitialRequiredToolName);
    }

    [TestMethod]
    public async Task AnswerAsync_ResearchReturnsText_StillRejectsInventedQuotations()
    {
        var sources = new SourceResearchTestData.Sources { InitialMiss = true };
        var registry = new AIToolRegistry([new SourceResearchAITools(sources, sources)]);
        var service = new GroundedAnswerService(sources, new ResearchEngine(false, true), Prompts(), new Audit(), new GroundedAnswerOptions { MaximumEnrichmentHits = 0 }, new FixedTime(), registry);

        var result = await service.AnswerAsync(new GroundedQuestion { Question = SourceResearchTestData.Question }, []);

        Assert.AreEqual(GroundedAnswerStatus.ValidationFailed, result.Status);
        Assert.IsNull(result.Answer);
    }

    private static GroundedPromptSet Prompts() => new()
    {
        SystemBehaviorPrompt = ReadPrompt("System"),
        PriorUserContextPrompt = "Prior user: " + GroundedPromptSet.ContextPlaceholder,
        PriorAssistantContextPrompt = "Prior assistant: " + GroundedPromptSet.ContextPlaceholder,
        CurrentQuestionInstruction = "Answer the current question.",
        EvidenceStartMarker = "BEGIN_EVIDENCE", EvidenceEndMarker = "END_EVIDENCE",
        ValidationRepairPrompt = ReadPrompt("Repair"),
        InterpretiveNotice = "One interpretation.", ResponseJsonSchema = "{\"type\":\"object\"}",
        SupportValidationPrompt = "Audit support.", SupportValidationJsonSchema = "{\"type\":\"object\"}",
    };

    private static string ReadPrompt(string name)
    {
        using var stream = typeof(GroundedAnswerResearchTests).Assembly.GetManifestResourceStream("ResearchPrompts." + name) ?? throw new AssertFailedException("Missing embedded production prompt.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private sealed class ResearchEngine(bool firstDraftDeflects, bool forgeQuotation = false, bool useInitialEvidence = false) : IAIEngine
    {
        private int calls;
        internal string? InitialRequiredToolName { get; private set; }
        public Task<AIEngineResult<T>> GenerateStructuredAsync<T>(IReadOnlyList<AIMessage> messages, string schemaName, BinaryData jsonSchema, CancellationToken cancellationToken = default) => throw new AssertFailedException("Expected bounded research access.");

        public async Task<AIEngineResult<T>> GenerateStructuredAsync<T>(IReadOnlyList<AIMessage> messages, string schemaName, BinaryData jsonSchema, AIToolExecutionSession toolSession, CancellationToken cancellationToken = default)
        {
            calls++;
            if (calls == 1)
            {
                InitialRequiredToolName = toolSession.RequiredToolName;
            }
            if (firstDraftDeflects && calls == 1)
            {
                return Success<T>(Draft("Nothing in the supplied halakhic passages ties the squash to a particular prayer.", "E1", "Musaf and shofar obligations."));
            }
            if (useInitialEvidence)
            {
                return Success<T>(Draft("The connection is wordplay: kra is paired with asking that our judgment be torn up and our merits called out.", "E1", SourceResearchTestData.Quotation));
            }
            await toolSession.ExecuteAsync("search_source_passages", BinaryData.FromString("{\"query\":\"gourd pumpkin Rosh Hashanah prayer\"}"), cancellationToken);
            using var response = JsonDocument.Parse(await toolSession.ExecuteAsync("read_source_passage", BinaryData.FromString("{\"reference\":\"Shulchan Arukh, Orach Chayim 583:1\"}"), cancellationToken));
            var item = response.RootElement.GetProperty("evidence")[0];
            var id = item.GetProperty("evidenceId").GetString() ?? throw new AssertFailedException("Missing evidence ID.");
            var quotation = forgeQuotation ? "This quotation was invented." : SourceResearchTestData.Quotation;
            return Success<T>(Draft("The connection is wordplay: kra is paired with asking that our judgment be torn up and our merits called out.", id, quotation));
        }

        private static GroundedAnswerDraft Draft(string text, string id, string quotation) => new()
        {
            Claims = [new GroundedClaimDraft { Text = text, EvidenceIds = [id], Quotations = [new GroundedQuotationDraft { EvidenceId = id, Text = quotation, Role = "Connects the food with the petitions." }] }],
            Disagreements = [], Limitations = [], HumanGuidanceRecommended = false,
        };

        private static AIEngineResult<T> Success<T>(GroundedAnswerDraft draft) => AIEngineResult<T>.Success((T)(object)draft, new AIResponseDiagnostics("test-response", "test", new AIUsage(1, 1, 2), TimeSpan.Zero, 1));
    }

    private sealed class Audit : IGroundedClaimEvidenceValidator
    {
        internal bool SawReligiousEvidence { get; private set; }
        public Task<ClaimEvidenceValidationResult> ValidateAsync(string questionContext, GroundedAnswerDraft draft, EvidencePacket packet, CancellationToken cancellationToken = default, ConversationPersonalization? personalization = null)
        {
            SawReligiousEvidence = packet.Items.Any(item => item.Source.CanonicalReference == SourceResearchTestData.Reference);
            return Task.FromResult(ClaimEvidenceValidationResult.Supported());
        }
    }

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => SourceResearchTestData.Now;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
