using System.Diagnostics;
using System.Text.Json;
using AskARabbiLIB.AI;
using AskARabbiLIB.AI.Tools;
using AskARabbiLIB.Calendar;
using AskARabbiLIB.Grounding;
using AskARabbiLIB.Models;
using AskARabbiLIB.Profiles;
using AskARabbiLIB.Retrieval;
using Azure.Core;

/// <summary>Runs explicit, billable live-provider conversation checks without modifying any user or chat records.</summary>
internal static class ConversationProbe
{
    internal static async Task<int> RunAsync(DocumentManifest manifest, string repositoryRoot, Uri endpoint, string model, string vectorStoreId, TokenCredential credential, IAzureOpenAIVectorStoreSearchClient searchClient, string questions, bool coreOnly, CancellationToken cancellationToken)
    {
        var answerOptions = new AIEngineOptions { ProjectEndpoint = endpoint, ModelName = model, ServiceTier = AIServiceTier.Priority, ReasoningEffort = AIReasoningEffort.Medium, MaximumOutputTokens = 8_000, MaximumRetryCount = 1 };
        var archive = new BundledNormalizedDocumentProvider(manifest, Path.Combine(repositoryRoot, "Backend", "AskARabbi.Api", "Data", "canonical-sources.zip"));
        await using var keywords = new SqliteSourceRetriever(Path.Combine(repositoryRoot, "Data", "canonical-search.sqlite"), archive.Manifest);
        var retriever = new ResearchSourceRetriever(new AzureOpenAIVectorStoreRetriever(searchClient, new AzureOpenAIVectorStoreRetrieverOptions { VectorStoreId = vectorStoreId, ExpectedCorpusFingerprint = SourceIndexBuilder.ComputeCorpusFingerprint(manifest) }, manifest), keywords);
        var reader = new BundledCanonicalSourceReader(manifest, Path.Combine(repositoryRoot, "Backend", "AskARabbi.Api", "Data", "canonical-sources.zip"));
        var registry = new DiagnosticRegistry(new AIToolRegistry([new CalendarAITools(new HebrewCalendarService()), new SourceResearchAITools(retriever, reader)]));
        var service = new GroundedAnswerService(retriever, new DiagnosticEngine(new AzureOpenAIEngine(answerOptions, credential)), new DiagnosticEngine(new AzureOpenAIEngine(answerOptions with { MaximumOutputTokens = 1_600, ReasoningEffort = AIReasoningEffort.Low }, credential)), GroundedPromptDirectoryLoader.Load(Path.Combine(repositoryRoot, "Prototype", "Prompts")), new GroundedAnswerOptions { MaximumCandidates = 20, MaximumEvidenceSegments = 10, MaximumEvidenceCharacters = 16_000, MaximumCharactersPerSegment = 2_400, MaximumSegmentsPerDocument = 3, MaximumEnrichmentHits = 0, RecentConversationTurns = 2 }, toolRegistry: registry, canonicalReader: reader);
        var history = new List<GroundedConversationTurn>();
        var exitCode = 0;
        foreach (var text in questions.Split("||", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var watch = Stopwatch.StartNew();
            var result = await service.AnswerAsync(new GroundedQuestion
            {
                Question = text,
                ConversationLanguage = "English",
                QuotationLanguage = "English",
                SourceKeys = coreOnly ? ["collection:Torah", "collection:Tanakh", "collection:Mishnah", "collection:Talmud"] : DocumentSourceCatalog.Create(manifest).Sources.Select(source => source.Key).ToArray(),
                ShouldGenerateConversationTitle = history.Count == 0,
                UserProfile = new UserProfile { Name = "QA", DateOfBirth = new DateOnly(2001, 12, 17), BirthTimeZone = "America/New_York", JewishHeritage = "Mizrahi" },
            }, history, cancellationToken).ConfigureAwait(false);
            var answer = result.Answer is null ? null : new GroundedAnswerTextRenderer().Render(result.Answer);
            Console.WriteLine(JsonSerializer.Serialize(new { question = text, status = result.Status.ToString(), seconds = watch.Elapsed.TotalSeconds, result.ErrorMessage, answer, sources = result.Answer?.Citations.Select(citation => citation.CanonicalReference), result.Trace }, new JsonSerializerOptions { WriteIndented = true }));
            if (answer is not null)
            {
                history.Add(new GroundedConversationTurn(text, answer));
            }
            else
            {
                exitCode = 1;
            }
        }
        return exitCode;
    }

    private sealed class DiagnosticEngine(IAIEngine inner) : IAIEngine
    {
        public async Task<AIEngineResult<T>> GenerateStructuredAsync<T>(IReadOnlyList<AIMessage> messages, string schemaName, BinaryData jsonSchema, CancellationToken cancellationToken = default)
        {
            var result = await inner.GenerateStructuredAsync<T>(messages, schemaName, jsonSchema, cancellationToken).ConfigureAwait(false);
            Console.WriteLine(JsonSerializer.Serialize(new { stage = schemaName, result.Value, result.ErrorMessage }));
            return result;
        }

        public async Task<AIEngineResult<T>> GenerateStructuredAsync<T>(IReadOnlyList<AIMessage> messages, string schemaName, BinaryData jsonSchema, AIToolExecutionSession toolSession, CancellationToken cancellationToken = default)
        {
            var result = await inner.GenerateStructuredAsync<T>(messages, schemaName, jsonSchema, toolSession, cancellationToken).ConfigureAwait(false);
            Console.WriteLine(JsonSerializer.Serialize(new { stage = schemaName, result.Value, result.ErrorMessage }));
            return result;
        }
    }

    private sealed class DiagnosticRegistry(IAIToolRegistry inner) : IAIToolRegistry
    {
        public IReadOnlyList<AIToolDefinition> Definitions => inner.Definitions;

        public bool MayApply(string question) => inner.MayApply(question);

        public async Task<AIToolExecutionResult> ExecuteAsync(string toolName, BinaryData arguments, AIToolExecutionContext context, CancellationToken cancellationToken = default)
        {
            var result = await inner.ExecuteAsync(toolName, arguments, context, cancellationToken).ConfigureAwait(false);
            // This explicitly invoked probe already prints its test question. Only expose the
            // proposed public canonical reference, never search arguments or profile data.
            using var parsed = JsonDocument.Parse(arguments);
            var attemptedReference = toolName == "read_source_passage" && parsed.RootElement.TryGetProperty("reference", out var reference) ? reference.GetString() : null;
            Console.WriteLine(JsonSerializer.Serialize(new { researchTool = toolName, attemptedReference, result.IsSuccess, references = result.Sources.Select(source => source.CanonicalReference).ToArray() }));
            return result;
        }
    }
}
