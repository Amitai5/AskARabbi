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
        var retriever = new AzureOpenAIVectorStoreRetriever(searchClient, new AzureOpenAIVectorStoreRetrieverOptions { VectorStoreId = vectorStoreId, ExpectedCorpusFingerprint = SourceIndexBuilder.ComputeCorpusFingerprint(manifest) }, manifest);
        var reader = new BundledCanonicalSourceReader(manifest, Path.Combine(repositoryRoot, "Backend", "AskARabbi.Api", "Data", "canonical-sources.zip"));
        var service = new GroundedAnswerService(retriever, new AzureOpenAIEngine(answerOptions, credential), new AzureOpenAIEngine(answerOptions with { MaximumOutputTokens = 3_200, ReasoningEffort = AIReasoningEffort.Low }, credential), GroundedPromptDirectoryLoader.Load(Path.Combine(repositoryRoot, "Prototype", "Prompts")), new GroundedAnswerOptions { MaximumCandidates = 20, MaximumEvidenceSegments = 10, MaximumEvidenceCharacters = 16_000, MaximumCharactersPerSegment = 2_400, MaximumSegmentsPerDocument = 3, MaximumEnrichmentHits = 0, RecentConversationTurns = 2 }, toolRegistry: new AIToolRegistry([new CalendarAITools(new HebrewCalendarService())]), canonicalReader: reader);
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
                SourceKeys = coreOnly ? ["collection:Torah", "collection:Tanakh", "collection:Mishnah", "collection:Talmud"] : [],
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
}
