using AskARabbiLIB.AI;
using AskARabbiLIB.Grounding;
using AskARabbiLIB.Models;
using AskARabbiLIB.Retrieval;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class AIGroundedClaimEvidenceValidatorTests
{
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ValidateAsync_RelevantSupportedEvaluation_ReturnsSupportedAndUsesIndependentSchema()
    {
        // Arrange
        var output = new GroundedSupportValidationDraft
        {
            IsResponsive = true,
            OverallExplanation = "The claim directly answers the current lamp-lighting question.",
            Evaluations = [new GroundedSupportEvaluationDraft { StatementId = "C1", IsRelevant = true, IsSupported = true, Explanation = "The cited passage directly states the timing rule.", SupportingQuotations = CreateDraft().Claims[0].Quotations }],
        };
        var engine = new FakeEngine(AIEngineResult<GroundedSupportValidationDraft>.Success(output, CreateDiagnostics()));
        var validator = new AIGroundedClaimEvidenceValidator(engine, CreatePrompts());

        // Act
        var result = await validator.ValidateAsync("What does the text say about lighting a lamp before Shabbat?", CreateDraft(), CreatePacket());

        // Assert
        Assert.AreEqual(ClaimEvidenceValidationStatus.Supported, result.Status);
        Assert.AreEqual("grounded_support_validation_v2", engine.LastSchemaName);
        Assert.IsNotNull(engine.LastMessages);
        StringAssert.Contains(engine.LastMessages[^1].Content, "A lamp may not be kindled.");
        StringAssert.Contains(engine.LastMessages[0].Content, "Independently");
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task ValidateAsync_UnsupportedEvaluation_ReturnsSpecificFailure()
    {
        // Arrange
        var output = new GroundedSupportValidationDraft
        {
            IsResponsive = true,
            OverallExplanation = "The draft attempts to answer the current question.",
            Evaluations = [new GroundedSupportEvaluationDraft { StatementId = "C1", IsRelevant = true, IsSupported = false, Explanation = "The quotation exists, but it does not establish the broader claim." }],
        };
        var engine = new FakeEngine(AIEngineResult<GroundedSupportValidationDraft>.Success(output, CreateDiagnostics()));
        var validator = new AIGroundedClaimEvidenceValidator(engine, CreatePrompts());

        // Act
        var result = await validator.ValidateAsync("Question", CreateDraft(), CreatePacket());

        // Assert
        Assert.AreEqual(ClaimEvidenceValidationStatus.Unsupported, result.Status);
        StringAssert.Contains(result.ErrorMessage, "does not establish");
    }

    [TestMethod]
    [TestCategory("Regression")]
    public async Task ValidateAsync_RuleRestatementDoesNotAnswerWhy_ReturnsUnsupported()
    {
        // Arrange
        var output = new GroundedSupportValidationDraft
        {
            IsResponsive = false,
            OverallExplanation = "The draft repeats that the rule is rabbinic but never gives the requested rationale.",
            Evaluations = [new GroundedSupportEvaluationDraft { StatementId = "C1", IsRelevant = true, IsSupported = true, Explanation = "The passage supports the rule classification." }],
        };
        var engine = new FakeEngine(AIEngineResult<GroundedSupportValidationDraft>.Success(output, CreateDiagnostics()));
        var validator = new AIGroundedClaimEvidenceValidator(engine, CreatePrompts());

        // Act
        var result = await validator.ValidateAsync("CURRENT QUESTION TO ANSWER:\nWhy did the rabbis choose that?", CreateDraft(), CreatePacket());

        // Assert
        Assert.AreEqual(ClaimEvidenceValidationStatus.Unsupported, result.Status);
        StringAssert.Contains(result.ErrorMessage, "did not directly answer");
        StringAssert.Contains(result.ErrorMessage, "never gives the requested rationale");
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task ValidateAsync_BlankOverallExplanation_ReturnsUnsupported()
    {
        // Arrange
        var output = new GroundedSupportValidationDraft
        {
            IsResponsive = true,
            OverallExplanation = " ",
            Evaluations = [new GroundedSupportEvaluationDraft { StatementId = "C1", IsRelevant = true, IsSupported = true, Explanation = "The passage supports the statement." }],
        };
        var engine = new FakeEngine(AIEngineResult<GroundedSupportValidationDraft>.Success(output, CreateDiagnostics()));
        var validator = new AIGroundedClaimEvidenceValidator(engine, CreatePrompts());

        // Act
        var result = await validator.ValidateAsync("Question", CreateDraft(), CreatePacket());

        // Assert
        Assert.AreEqual(ClaimEvidenceValidationStatus.Unsupported, result.Status);
        StringAssert.Contains(result.ErrorMessage, "overall responsiveness explanation");
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task ValidateAsync_MissingStatementEvaluation_ReturnsUnsupported()
    {
        // Arrange
        var output = new GroundedSupportValidationDraft { IsResponsive = true, OverallExplanation = "The draft attempts to answer the question.", Evaluations = [] };
        var engine = new FakeEngine(AIEngineResult<GroundedSupportValidationDraft>.Success(output, CreateDiagnostics()));
        var validator = new AIGroundedClaimEvidenceValidator(engine, CreatePrompts());

        // Act
        var result = await validator.ValidateAsync("Question", CreateDraft(), CreatePacket());

        // Assert
        Assert.AreEqual(ClaimEvidenceValidationStatus.Unsupported, result.Status);
        StringAssert.Contains(result.ErrorMessage, "every statement");
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task ValidateAsync_ProviderFailure_ReturnsTypedProviderFailure()
    {
        // Arrange
        var failure = AIEngineResult<GroundedSupportValidationDraft>.Failure(AIEngineStatus.TimedOut, "Timed out.", CreateDiagnostics());
        var engine = new FakeEngine(failure);
        var validator = new AIGroundedClaimEvidenceValidator(engine, CreatePrompts());

        // Act
        var result = await validator.ValidateAsync("Question", CreateDraft(), CreatePacket());

        // Assert
        Assert.AreEqual(ClaimEvidenceValidationStatus.ProviderFailure, result.Status);
        Assert.AreEqual(AIEngineStatus.TimedOut, result.EngineStatus);
    }

    [TestMethod]
    [TestCategory("Regression")]
    public async Task ValidateAsync_SupportExistsInAnotherPassage_ReconcilesCitationsWithoutChangingClaim()
    {
        var packet = CreatePacket();
        var other = packet.Items[0].Source with { SegmentId = "other", CanonicalReference = "Shabbat 20a:2", Text = "This other passage concerns the lamp wick." };
        packet = new EvidencePacket([.. packet.Items, new EvidenceItem("E2", other, other.Text, false, other.Text.Length)], packet.CharacterCount + other.Text.Length);
        var draft = CreateDraft();
        draft = draft with { Claims = [draft.Claims[0] with { EvidenceIds = ["E2"], Quotations = [new GroundedQuotationDraft { EvidenceId = "E2", Text = "@Q1", Role = "Proposed support" }] }] };
        var output = new GroundedSupportValidationDraft
        {
            IsResponsive = true,
            OverallExplanation = "The timing explanation answers the question.",
            Evaluations = [new GroundedSupportEvaluationDraft { StatementId = "C1", IsRelevant = true, IsSupported = true, Explanation = "E1, not E2, establishes the timing.", SupportingQuotations = [new GroundedQuotationDraft { EvidenceId = "E1", Text = "@Q2", Role = "The flame must catch before nightfall." }] }],
        };
        var engine = new FakeEngine(AIEngineResult<GroundedSupportValidationDraft>.Success(output, CreateDiagnostics()));

        var result = await new AIGroundedClaimEvidenceValidator(engine, CreatePrompts()).ValidateAsync("When must the flame catch?", draft, packet);

        Assert.AreEqual(ClaimEvidenceValidationStatus.Supported, result.Status);
        Assert.IsNotNull(result.ReconciledDraft);
        Assert.AreEqual(draft.Claims[0].Text, result.ReconciledDraft.Claims[0].Text);
        CollectionAssert.AreEqual(new[] { "E1" }, result.ReconciledDraft.Claims[0].EvidenceIds.ToArray());
        Assert.AreEqual("The flame should catch before nightfall.", result.ReconciledDraft.Claims[0].Quotations[0].Text);
        StringAssert.Contains(engine.LastMessages![^1].Content, "A lamp may not be kindled.");
    }

    [TestMethod]
    [DataRow("E1", "invented quotation")]
    [DataRow("E2", "@Q1")]
    [DataRow("E1", "@Q99")]
    [DataRow(null, "@Q1")]
    [DataRow("E1", null)]
    [TestCategory("Regression")]
    public async Task ValidateAsync_AuditReturnsInvalidCitation_FailsClosed(string? evidenceId, string? text)
    {
        var output = new GroundedSupportValidationDraft
        {
            IsResponsive = true,
            OverallExplanation = "The draft attempts to answer.",
            Evaluations = [new GroundedSupportEvaluationDraft { StatementId = "C1", IsRelevant = true, IsSupported = true, Explanation = "Claims support.", SupportingQuotations = [new GroundedQuotationDraft { EvidenceId = evidenceId!, Text = text!, Role = "Basis" }] }],
        };
        var engine = new FakeEngine(AIEngineResult<GroundedSupportValidationDraft>.Success(output, CreateDiagnostics()));

        var result = await new AIGroundedClaimEvidenceValidator(engine, CreatePrompts()).ValidateAsync("Question", CreateDraft(), CreatePacket());

        Assert.AreEqual(ClaimEvidenceValidationStatus.Unsupported, result.Status);
        Assert.IsNull(result.ReconciledDraft);
    }

    [TestMethod]
    [TestCategory("Regression")]
    public async Task ValidateAsync_AuditOmitsSupportingQuotations_FailsClosed()
    {
        var output = new GroundedSupportValidationDraft
        {
            IsResponsive = true,
            OverallExplanation = "The draft attempts to answer.",
            Evaluations = [new GroundedSupportEvaluationDraft { StatementId = "C1", IsRelevant = true, IsSupported = true, Explanation = "Claims support." }],
        };
        var engine = new FakeEngine(AIEngineResult<GroundedSupportValidationDraft>.Success(output, CreateDiagnostics()));

        var result = await new AIGroundedClaimEvidenceValidator(engine, CreatePrompts()).ValidateAsync("Question", CreateDraft(), CreatePacket());

        Assert.AreEqual(ClaimEvidenceValidationStatus.Unsupported, result.Status);
        Assert.IsNull(result.ReconciledDraft);
    }

    private static GroundedPromptSet CreatePrompts() => new()
    {
        SystemBehaviorPrompt = "Use evidence.",
        PriorUserContextPrompt = $"Prior user: {GroundedPromptSet.ContextPlaceholder}",
        PriorAssistantContextPrompt = $"Prior assistant: {GroundedPromptSet.ContextPlaceholder}",
        CurrentQuestionInstruction = "Answer from evidence.",
        EvidenceStartMarker = "BEGIN_EVIDENCE",
        EvidenceEndMarker = "END_EVIDENCE",
        ValidationRepairPrompt = $"Repair: {GroundedPromptSet.ValidationErrorPlaceholder}",
        InterpretiveNotice = "One interpretation.",
        ResponseJsonSchema = "{\"type\":\"object\"}",
        SupportValidationPrompt = "Independently evaluate relevance and support.",
        SupportValidationJsonSchema = "{\"type\":\"object\"}",
    };

    private static GroundedAnswerDraft CreateDraft() => new()
    {
        Claims =
        [
            new GroundedClaimDraft
            {
                Text = "The passage discusses when a lamp may be kindled before Shabbat.",
                EvidenceIds = ["E1"],
                Attribution = "The Shabbat passage",
                Quotations = [new GroundedQuotationDraft { EvidenceId = "E1", Text = "A lamp may not be kindled.", Role = "Direct basis" }],
            },
        ],
        Disagreements = [],
        Limitations = [],
        ClarifyingQuestion = null,
        HumanGuidanceRecommended = false,
    };

    private static EvidencePacket CreatePacket()
    {
        var segment = new SourceSegment
        {
            SegmentId = "sefaria:shabbat:segment:1",
            DocumentId = "sefaria:shabbat",
            CanonicalReference = "Shabbat 20a:1",
            DocumentOrdinal = 0,
            Text = "A lamp may not be kindled. The flame should catch before nightfall.",
            Title = "Shabbat",
            HebrewTitle = "שבת",
            Language = "English",
            LanguageCode = "en",
            Collection = "Talmud",
            Categories = ["Talmud", "Bavli", "Seder Moed"],
            Version = "Test",
            License = "CC-BY",
            LicenseCategory = SourceLicenseCategory.CcBy,
            SourceUrl = "https://example.test/shabbat",
            FilePath = "Data/Test.md",
        };
        return new EvidencePacket([new EvidenceItem("E1", segment, segment.Text, false, segment.Text.Length)], segment.Text.Length);
    }

    private static AIResponseDiagnostics CreateDiagnostics() => new("response-id", "test-model", new AIUsage(10, 5, 15), TimeSpan.FromMilliseconds(5), 1);

    private sealed class FakeEngine : IAIEngine
    {
        private readonly object result;

        internal FakeEngine(object result)
        {
            this.result = result;
        }

        internal IReadOnlyList<AIMessage>? LastMessages { get; private set; }

        internal string? LastSchemaName { get; private set; }

        public Task<AIEngineResult<T>> GenerateStructuredAsync<T>(IReadOnlyList<AIMessage> messages, string schemaName, BinaryData jsonSchema, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastMessages = messages.ToArray();
            LastSchemaName = schemaName;
            return Task.FromResult((AIEngineResult<T>)result);
        }
    }
}
