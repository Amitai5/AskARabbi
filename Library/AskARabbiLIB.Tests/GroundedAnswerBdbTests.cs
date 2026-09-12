using System.Text.Json;
using AskARabbiLIB.AI;
using AskARabbiLIB.AI.Tools;
using AskARabbiLIB.Grounding;
using AskARabbiLIB.Retrieval;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class GroundedAnswerBdbTests
{
    [TestMethod]
    [DataRow("What does this Hebrew word mean?", "English", "English")]
    [DataRow("קרא", "English", "Hebrew")]
    [DataRow("מה משמעות המילה קרא?", "Hebrew", "Hebrew")]
    public async Task AnswerAsync_NoReligiousPassages_UsesDictionaryAndPreservesCitation(string question, string responseLanguage, string quotationLanguage)
    {
        var registry = new AIToolRegistry([new BdbDictionaryAITools(new LexiconTestData.Store(LexiconTestData.Entry()))]);
        var engine = new DictionaryEngine();
        var audit = new Audit();
        var service = new GroundedAnswerService(new EmptyRetriever(), engine, Prompts(), audit, new GroundedAnswerOptions { MaximumEnrichmentHits = 0 }, new FixedTime(), registry);

        var result = await service.AnswerAsync(new GroundedQuestion { Question = question, ConversationLanguage = responseLanguage, QuotationLanguage = quotationLanguage }, []);

        Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
        Assert.AreEqual(1, engine.Calls);
        Assert.IsTrue(audit.SawDictionaryEvidence);
        Assert.IsNotNull(result.Answer);
        Assert.AreEqual("Dictionaries", result.Answer.Citations[0].Collection);
        Assert.AreEqual(LexiconTestData.Entry().SourceUrl, result.Answer.Citations[0].SourceUrl);
        Assert.AreEqual(responseLanguage, result.Answer.ResponseLanguage);
    }

    [TestMethod]
    public async Task AnswerAsync_InventedDictionaryQuotation_IsRejectedDespiteSuccessfulLookup()
    {
        var registry = new AIToolRegistry([new BdbDictionaryAITools(new LexiconTestData.Store(LexiconTestData.Entry()))]);
        var service = new GroundedAnswerService(new EmptyRetriever(), new DictionaryEngine(true), Prompts(), new Audit(), new GroundedAnswerOptions { MaximumEnrichmentHits = 0 }, new FixedTime(), registry);

        var result = await service.AnswerAsync(new GroundedQuestion { Question = "What does this Hebrew word mean?" }, []);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(GroundedAnswerStatus.ValidationFailed, result.Status);
    }

    private static GroundedPromptSet Prompts() => new()
    {
        SystemBehaviorPrompt = "Use supplied original sources.",
        PriorUserContextPrompt = "Prior user: " + GroundedPromptSet.ContextPlaceholder,
        PriorAssistantContextPrompt = "Prior assistant: " + GroundedPromptSet.ContextPlaceholder,
        CurrentQuestionInstruction = "Use dictionary entries when needed.",
        EvidenceStartMarker = "BEGIN_EVIDENCE",
        EvidenceEndMarker = "END_EVIDENCE",
        ValidationRepairPrompt = "Repair: " + GroundedPromptSet.ValidationErrorPlaceholder,
        InterpretiveNotice = "One interpretation.",
        ResponseJsonSchema = "{\"type\":\"object\"}",
        SupportValidationPrompt = "Audit source support.",
        SupportValidationJsonSchema = "{\"type\":\"object\"}",
    };

    private sealed class EmptyRetriever : ISourceRetriever
    {
        public Task<IReadOnlyList<SourceRetrievalHit>> SearchAsync(SourceRetrievalQuery query, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SourceRetrievalHit>>([]);
        public Task<IReadOnlyList<SourceSegment>> GetContextAsync(string documentId, int documentOrdinal, int radius, CancellationToken cancellationToken = default) => throw new AssertFailedException("No unrelated source context should be requested.");
    }

    private sealed class DictionaryEngine(bool forgeQuotation = false) : IAIEngine
    {
        internal int Calls { get; private set; }
        public Task<AIEngineResult<T>> GenerateStructuredAsync<T>(IReadOnlyList<AIMessage> messages, string schemaName, BinaryData jsonSchema, CancellationToken cancellationToken = default) => throw new AssertFailedException("The dictionary must be available to the model.");
        public async Task<AIEngineResult<T>> GenerateStructuredAsync<T>(IReadOnlyList<AIMessage> messages, string schemaName, BinaryData jsonSchema, AIToolExecutionSession toolSession, CancellationToken cancellationToken = default)
        {
            Calls++;
            var output = await toolSession.ExecuteAsync("search_bdb_dictionary", BinaryData.FromString("{\"query\":\"קרא\"}"), cancellationToken);
            using var document = JsonDocument.Parse(output);
            var evidence = document.RootElement.GetProperty("evidence")[0];
            var id = evidence.GetProperty("evidenceId").GetString() ?? throw new AssertFailedException("Missing evidence ID.");
            var quote = forgeQuotation ? "A fabricated definition absent from this dictionary." : evidence.GetProperty("exactText").GetString() ?? throw new AssertFailedException("Missing definition.");
            var draft = new GroundedAnswerDraft
            {
                Claims = [new GroundedClaimDraft { Text = "The word can mean call, proclaim, or read in the stated biblical context.", EvidenceIds = [id], Quotations = [new GroundedQuotationDraft { EvidenceId = id, Text = quote, Role = "Gives the lexical meaning." }] }],
                Disagreements = [], Limitations = [], HumanGuidanceRecommended = false,
            };
            return AIEngineResult<T>.Success((T)(object)draft, new AIResponseDiagnostics("test-response", "test", new AIUsage(1, 1, 2), TimeSpan.Zero, 1));
        }
    }

    private sealed class Audit : IGroundedClaimEvidenceValidator
    {
        internal bool SawDictionaryEvidence { get; private set; }
        public Task<ClaimEvidenceValidationResult> ValidateAsync(string questionContext, GroundedAnswerDraft draft, EvidencePacket packet, CancellationToken cancellationToken = default, ConversationPersonalization? personalization = null)
        {
            SawDictionaryEvidence = packet.Items.Any(item => item.Source.Collection == "Dictionaries");
            return Task.FromResult(ClaimEvidenceValidationResult.Supported());
        }
    }

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 9, 12, 12, 0, 0, TimeSpan.Zero);
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
