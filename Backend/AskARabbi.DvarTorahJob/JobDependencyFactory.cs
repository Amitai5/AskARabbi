using AskARabbiLIB;
using AskARabbiLIB.AI;
using AskARabbiLIB.Calendar;
using AskARabbiLIB.CurrentEvents;
using AskARabbiLIB.DvarTorah;
using AskARabbiLIB.DvarTorah.Audio;
using AskARabbiLIB.Persistence.Mongo;
using AskARabbiLIB.Retrieval;
using Azure.Core;
using Azure.Identity;
using MongoDB.Driver;

namespace AskARabbi.DvarTorahJob;

internal static class JobDependencyFactory
{
    private static readonly HttpClient NewsHttpClient = new() { Timeout = Timeout.InfiniteTimeSpan };
    private static readonly HttpClient AzureVectorStoreHttpClient = new() { Timeout = Timeout.InfiniteTimeSpan };

    internal static async Task<WeeklyDvarTorahGenerationResult> GenerateAsync(string invocationId, CancellationToken cancellationToken)
    {
        var coordinator = await CreateCoordinatorAsync(cancellationToken).ConfigureAwait(false);
        return await coordinator.RunAsync(invocationId, cancellationToken).ConfigureAwait(false);
    }

    internal static Task<WeeklyDvarTorahArticle?> LoadPublishedAsync(string weekKey, CancellationToken cancellationToken)
    {
        if (!DvarTorahJobEnvironment.GetBoolean("DvarTorahAudio__Enabled", false))
        {
            throw new DvarTorahJobConfigurationException("DvarTorahAudio__Enabled must be true for an audio-only backfill.");
        }

        var database = CreateDatabase(out var options);
        return new MongoWeeklyDvarTorahStore(database, options).GetPublishedByWeekKeyAsync(weekKey, cancellationToken);
    }

    internal static async Task<WeeklyDvarTorahAudioResult> GenerateAudioAsync(WeeklyDvarTorahArticle article, string invocationId, CancellationToken cancellationToken)
    {
        if (!DvarTorahJobEnvironment.GetBoolean("DvarTorahAudio__Enabled", false))
        {
            return new WeeklyDvarTorahAudioResult(WeeklyDvarTorahAudioStatus.Disabled, null);
        }

        var options = new DvarTorahAudioOptions
        {
            Enabled = true,
            StorageServiceUri = DvarTorahJobEnvironment.GetRequired("DvarTorahAudio__StorageServiceUri"),
            ContainerName = DvarTorahJobEnvironment.GetOptional("DvarTorahAudio__ContainerName") ?? "dvar-torah-audio",
            SpeechRegion = DvarTorahJobEnvironment.GetOptional("DvarTorahAudio__SpeechRegion") ?? "eastus2",
            SpeechResourceId = DvarTorahJobEnvironment.GetRequired("DvarTorahAudio__SpeechResourceId"),
            Voice = DvarTorahJobEnvironment.GetOptional("DvarTorahAudio__Voice") ?? "en-US-AndrewMultilingualNeural",
            FfmpegPath = DvarTorahJobEnvironment.GetOptional("DvarTorahAudio__FfmpegPath") ?? "ffmpeg",
            LeaseDuration = TimeSpan.FromMinutes(DvarTorahJobEnvironment.GetInteger("DvarTorahAudio__LeaseMinutes", 30)),
        };
        options.ValidateGeneration();

        var credential = CreateCredential();
        var database = CreateDatabase(out var databaseOptions);
        var store = new MongoWeeklyDvarTorahAudioStore(database, databaseOptions);
        var narrator = new AzureSpeechDvarTorahNarrator(options, credential, new FfmpegDvarTorahMp3Encoder(options));
        var storage = new AzureBlobDvarTorahAudioStorage(options, credential);
        var coordinator = new WeeklyDvarTorahAudioCoordinator(store, narrator, storage, TimeProvider.System, options);
        return await coordinator.RunAsync(article, invocationId, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<WeeklyDvarTorahGenerationCoordinator> CreateCoordinatorAsync(CancellationToken cancellationToken)
    {
        var dvarTorahOptions = new WeeklyDvarTorahOptions
        {
            InIsrael = DvarTorahJobEnvironment.GetBoolean("DvarTorah__InIsrael", false),
            GenerationLeaseMinutes = DvarTorahJobEnvironment.GetInteger("DvarTorah__GenerationLeaseMinutes", 30),
        };
        dvarTorahOptions.Validate();

        var contentOptions = new WeeklyDvarTorahContentOptions
        {
            ResearchWindowDays = DvarTorahJobEnvironment.GetInteger("DvarTorah__ResearchWindowDays", 7),
            MaximumNewsCandidates = DvarTorahJobEnvironment.GetInteger("DvarTorah__MaximumNewsCandidates", 80),
            MinimumNewsPublishers = DvarTorahJobEnvironment.GetInteger("DvarTorah__MinimumNewsPublishers", 2),
            MaximumNewsSources = DvarTorahJobEnvironment.GetInteger("DvarTorah__MaximumNewsSources", 4),
            MinimumTorahEvidenceItems = DvarTorahJobEnvironment.GetInteger("DvarTorah__MinimumTorahEvidenceItems", 8),
            MaximumTorahEvidenceItems = DvarTorahJobEnvironment.GetInteger("DvarTorah__MaximumTorahEvidenceItems", 14),
            MinimumTorahGroundingPercent = DvarTorahJobEnvironment.GetInteger("DvarTorah__MinimumTorahGroundingPercent", 80),
            MinimumBodyCharacters = DvarTorahJobEnvironment.GetInteger("DvarTorah__MinimumBodyCharacters", 2_500),
            MaximumBodyCharacters = DvarTorahJobEnvironment.GetInteger("DvarTorah__MaximumBodyCharacters", 15_000),
            OverallTimeout = TimeSpan.FromMinutes(DvarTorahJobEnvironment.GetInteger("DvarTorah__ResearchTimeoutMinutes", 25)),
            GeneratorVersion = DvarTorahJobEnvironment.GetOptional("DvarTorah__GeneratorVersion") ?? "weekly-dvar-torah-v2",
        };
        contentOptions.Validate();

        var projectEndpoint = new Uri(DvarTorahJobEnvironment.GetRequired("AI__ProjectEndpoint"), UriKind.Absolute);
        var modelName = DvarTorahJobEnvironment.GetRequired("AI__ModelName");
        var vectorStoreId = DvarTorahJobEnvironment.GetRequired("AI__VectorStoreId");
        var corpusFingerprint = DvarTorahJobEnvironment.GetRequired("AI__CorpusFingerprint");
        var timeoutSeconds = DvarTorahJobEnvironment.GetInteger("AI__TimeoutSeconds", 300);
        var retryCount = DvarTorahJobEnvironment.GetInteger("AI__MaximumRetryCount", 1);
        var credential = CreateCredential();
        var manifest = await new ManifestLoader().LoadAsync(Path.Combine(AppContext.BaseDirectory, "Data", "document-manifest.json"), cancellationToken).ConfigureAwait(false);
        var vectorClient = new AzureOpenAIVectorStoreClient(
            new AzureOpenAIVectorStoreClientOptions
            {
                ProjectEndpoint = projectEndpoint,
                ModelName = modelName,
                Timeout = TimeSpan.FromSeconds(timeoutSeconds),
            },
            credential,
            AzureVectorStoreHttpClient);
        var retriever = new AzureOpenAIVectorStoreRetriever(
            vectorClient,
            new AzureOpenAIVectorStoreRetrieverOptions
            {
                VectorStoreId = vectorStoreId,
                ExpectedCorpusFingerprint = corpusFingerprint,
                ScoreThreshold = 0,
            },
            manifest);
        var reasoningEffort = DvarTorahJobEnvironment.GetEnum("AI__ReasoningEffort", AIReasoningEffort.High);
        var validationReasoningEffort = DvarTorahJobEnvironment.GetEnum("AI__ValidationReasoningEffort", reasoningEffort);
        var generationEngine = new AzureOpenAIEngine(new AIEngineOptions
        {
            ProjectEndpoint = projectEndpoint,
            ModelName = modelName,
            Timeout = TimeSpan.FromSeconds(timeoutSeconds),
            MaximumOutputTokens = DvarTorahJobEnvironment.GetInteger("AI__MaximumOutputTokens", 24_000),
            ReasoningEffort = reasoningEffort,
            MaximumRetryCount = retryCount,
        }, credential);
        var reviewEngine = new AzureOpenAIEngine(new AIEngineOptions
        {
            ProjectEndpoint = projectEndpoint,
            ModelName = modelName,
            Timeout = TimeSpan.FromSeconds(timeoutSeconds),
            MaximumOutputTokens = DvarTorahJobEnvironment.GetInteger("AI__ValidationMaximumOutputTokens", 12_000),
            ReasoningEffort = validationReasoningEffort,
            MaximumRetryCount = retryCount,
        }, credential);
        var prompts = WeeklyDvarTorahPromptDirectoryLoader.Load(Path.Combine(AppContext.BaseDirectory, "Prompts"));
        var currentEvents = new FreeRssCurrentEventsSource(NewsHttpClient, FreeNewsFeedCatalog.Default, timeProvider: TimeProvider.System, feedFailureObserver: DvarTorahJobLog.NewsFeedFailed);
        var generator = new GroundedWeeklyDvarTorahGenerator(currentEvents, retriever, generationEngine, reviewEngine, prompts, contentOptions, TimeProvider.System);

        var database = CreateDatabase(out var databaseOptions);
        var store = new MongoWeeklyDvarTorahStore(database, databaseOptions);
        var timeProvider = TimeProvider.System;
        var weeklyService = new WeeklyDvarTorahService(new HebrewCalendarService(), store, timeProvider, dvarTorahOptions);
        return new WeeklyDvarTorahGenerationCoordinator(store, generator, weeklyService, timeProvider, dvarTorahOptions);
    }

    private static IMongoDatabase CreateDatabase(out MongoDatabaseOptions options)
    {
        options = new MongoDatabaseOptions
        {
            ConnectionString = DvarTorahJobEnvironment.GetRequired("MongoDB__ConnectionString"),
            DatabaseName = DvarTorahJobEnvironment.GetOptional("MongoDB__DatabaseName") ?? "askarabbi",
            DvarTorahCollectionName = DvarTorahJobEnvironment.GetOptional("MongoDB__DvarTorahCollectionName") ?? "WeeklyAIDvarTorahs",
        };
        options.Validate();
        var client = new MongoClient(MongoClientSettings.FromConnectionString(options.ConnectionString));
        return client.GetDatabase(options.DatabaseName);
    }

    private static TokenCredential CreateCredential()
    {
        var environment = DvarTorahJobEnvironment.GetOptional("DOTNET_ENVIRONMENT") ?? DvarTorahJobEnvironment.GetOptional("ASPNETCORE_ENVIRONMENT");
        return string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase)
            ? new DefaultAzureCredential(new DefaultAzureCredentialOptions { TenantId = DvarTorahJobEnvironment.GetOptional("AI__TenantId") })
            : new ManagedIdentityCredential(ManagedIdentityId.SystemAssigned);
    }
}
