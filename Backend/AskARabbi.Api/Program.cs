using System.Text.Json.Serialization;
using AskARabbi.Api.Authentication;
using AskARabbi.Api.Configuration;
using AskARabbi.Api.Conversations;
using AskARabbi.Api.Development;
using AskARabbi.Api.DvarTorahAudio;
using AskARabbi.Api.Errors;
using AskARabbi.Api.Persistence;
using AskARabbi.Api.Usage;
using AskARabbiLIB;
using AskARabbiLIB.AI;
using AskARabbiLIB.AI.Tools;
using AskARabbiLIB.Calendar;
using AskARabbiLIB.Conversations;
using AskARabbiLIB.ConversationSettings;
using AskARabbiLIB.DvarTorah;
using AskARabbiLIB.Grounding;
using AskARabbiLIB.Retrieval;
using AskARabbiLIB.Usage;
using Azure.Core;
using Azure.Identity;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.ResponseCompression;

var builder = WebApplication.CreateBuilder(args);

var localDevelopmentOptions = builder.Configuration.GetSection(LocalDevelopmentOptions.SectionName).Get<LocalDevelopmentOptions>() ?? new LocalDevelopmentOptions();
localDevelopmentOptions.Validate(builder.Environment.EnvironmentName);
var allowedFrontendOrigins = (builder.Configuration.GetSection(FrontendCorsOptions.SectionName).Get<FrontendCorsOptions>() ?? new FrontendCorsOptions()).GetAllowedOrigins(builder.Environment.IsDevelopment());
if (localDevelopmentOptions.UseDemoServices)
{
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole();
    builder.Logging.AddDebug();
}

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddHealthChecks().AddCheck<SessionProtectionHealthCheck>("session-protection");
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(["application/problem+json"]);
});
builder.Services.AddControllers().AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ConversationService>();
builder.Services.AddScoped<ConversationSettingsService>();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddScoped<WorkOsCookieAuthenticationEvents>();
builder.Services.AddCors(options => options.AddPolicy(FrontendCorsOptions.PolicyName, policy =>
{
    policy.AllowAnyHeader().AllowAnyMethod().AllowCredentials().WithExposedHeaders("Server-Timing", "Accept-Ranges", "Content-Range", "ETag");
    if (allowedFrontendOrigins.Count > 0)
    {
        policy.WithOrigins([.. allowedFrontendOrigins]);
    }
    else
    {
        policy.SetIsOriginAllowed(_ => false);
    }
}));

var usageOptions = builder.Configuration.GetSection(MonthlyUsageOptions.SectionName).Get<MonthlyUsageOptions>() ?? new MonthlyUsageOptions();
usageOptions.Validate();
builder.Services.AddSingleton(usageOptions);
builder.Services.AddScoped(provider => new MonthlyUsageService(provider.GetRequiredService<IUsageStore>(), usageOptions.MonthlyAnswerLimit, provider.GetRequiredService<TimeProvider>()));

var groundedChatOptions = builder.Configuration.GetSection(GroundedChatOptions.SectionName).Get<GroundedChatOptions>() ?? new GroundedChatOptions();
groundedChatOptions.Validate();
builder.Services.AddSingleton(groundedChatOptions);
builder.Services.AddSingleton<GroundedAnswerTextRenderer>();
builder.Services.AddSingleton<IHebrewCalendarService, HebrewCalendarService>();
builder.Services.AddSingleton<CalendarAITools>();
builder.Services.AddSingleton<IAIToolRegistry>(provider => new AIToolRegistry([provider.GetRequiredService<CalendarAITools>()]));
var weeklyDvarTorahOptions = builder.Configuration.GetSection(WeeklyDvarTorahOptions.SectionName).Get<WeeklyDvarTorahOptions>() ?? new WeeklyDvarTorahOptions();
weeklyDvarTorahOptions.Validate();
builder.Services.AddSingleton(weeklyDvarTorahOptions);
builder.Services.AddSingleton<WeeklyDvarTorahService>();
builder.Services.AddDvarTorahAudio(builder.Configuration, builder.Environment);
if (groundedChatOptions.IsConfigured)
{
    var managedManifestPath = Path.Combine(AppContext.BaseDirectory, "Data", "document-manifest.json");
    var managedManifest = await new ManifestLoader().LoadAsync(managedManifestPath).ConfigureAwait(false);
    builder.Services.AddSingleton(managedManifest);
    builder.Services.AddSingleton<ICanonicalSourceReader>(new BundledCanonicalSourceReader(managedManifest, Path.Combine(AppContext.BaseDirectory, "Data", "canonical-sources.zip")));
    builder.Services.AddSingleton<TokenCredential>(_ => builder.Environment.IsDevelopment()
        ? new DefaultAzureCredential(new DefaultAzureCredentialOptions { TenantId = string.IsNullOrWhiteSpace(groundedChatOptions.TenantId) ? null : groundedChatOptions.TenantId })
        : new ManagedIdentityCredential(ManagedIdentityId.SystemAssigned));
    builder.Services.AddHttpClient("AzureOpenAIVectorStore", client => client.Timeout = Timeout.InfiniteTimeSpan);
    builder.Services.AddSingleton(provider => new AzureOpenAIVectorStoreClient(
        new AzureOpenAIVectorStoreClientOptions
        {
            ProjectEndpoint = new Uri(groundedChatOptions.ProjectEndpoint, UriKind.Absolute),
            ModelName = groundedChatOptions.ModelName,
            ServiceTier = groundedChatOptions.ServiceTier,
            Timeout = TimeSpan.FromSeconds(groundedChatOptions.TimeoutSeconds),
        },
        provider.GetRequiredService<TokenCredential>(),
        provider.GetRequiredService<IHttpClientFactory>().CreateClient("AzureOpenAIVectorStore")));
    builder.Services.AddSingleton<IAzureOpenAIVectorStoreSearchClient>(provider => provider.GetRequiredService<AzureOpenAIVectorStoreClient>());
    builder.Services.AddSingleton<ISourceRetriever>(provider => new CachingSourceRetriever(
        new AzureOpenAIVectorStoreRetriever(
            provider.GetRequiredService<IAzureOpenAIVectorStoreSearchClient>(),
            new AzureOpenAIVectorStoreRetrieverOptions
            {
                VectorStoreId = groundedChatOptions.VectorStoreId,
                ExpectedCorpusFingerprint = groundedChatOptions.CorpusFingerprint,
                ScoreThreshold = groundedChatOptions.RetrievalScoreThreshold,
            },
            provider.GetRequiredService<AskARabbiLIB.Models.DocumentManifest>()),
        groundedChatOptions.CreateRetrieverCacheOptions(),
        provider.GetRequiredService<TimeProvider>()));
    builder.Services.AddSingleton<IAIEngine>(provider => new AzureOpenAIEngine(
        new AIEngineOptions
        {
            ProjectEndpoint = new Uri(groundedChatOptions.ProjectEndpoint, UriKind.Absolute),
            ModelName = groundedChatOptions.ModelName,
            Timeout = TimeSpan.FromSeconds(groundedChatOptions.TimeoutSeconds),
            MaximumOutputTokens = groundedChatOptions.MaximumOutputTokens,
            ReasoningEffort = groundedChatOptions.ReasoningEffort,
            ServiceTier = groundedChatOptions.ServiceTier,
            MaximumRetryCount = groundedChatOptions.MaximumRetryCount,
        },
        provider.GetRequiredService<TokenCredential>()));
    var groundedPrompts = GroundedPromptDirectoryLoader.Load(Path.Combine(AppContext.BaseDirectory, "Prompts"));
    builder.Services.AddSingleton(groundedPrompts);
    builder.Services.AddSingleton<IGroundedAnswerService>(provider =>
    {
        var validationEngine = new AzureOpenAIEngine(
            new AIEngineOptions
            {
                ProjectEndpoint = new Uri(groundedChatOptions.ProjectEndpoint, UriKind.Absolute),
                ModelName = groundedChatOptions.ModelName,
                Timeout = TimeSpan.FromSeconds(groundedChatOptions.TimeoutSeconds),
                MaximumOutputTokens = groundedChatOptions.ValidationMaximumOutputTokens,
                ReasoningEffort = AIReasoningEffort.Low,
                ServiceTier = groundedChatOptions.ServiceTier,
                MaximumRetryCount = groundedChatOptions.MaximumRetryCount,
            },
            provider.GetRequiredService<TokenCredential>());
        return new GroundedAnswerService(
            provider.GetRequiredService<ISourceRetriever>(),
            provider.GetRequiredService<IAIEngine>(),
            validationEngine,
            provider.GetRequiredService<GroundedPromptSet>(),
            groundedChatOptions.CreateGroundedAnswerOptions(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<IAIToolRegistry>(),
            provider.GetRequiredService<ICanonicalSourceReader>());
    });
}
else
{
    builder.Services.AddSingleton<IGroundedAnswerService, UnavailableGroundedAnswerService>();
}
builder.Services.AddScoped<GroundedConversationTurnService>();

if (localDevelopmentOptions.UseDemoServices)
{
    builder.Services.AddAskRabbiLocalDevelopment(builder.Configuration);
}
else
{
    builder.Services.AddAskRabbiPersistence(builder.Configuration);
    builder.Services.AddAskRabbiAuthentication(builder.Configuration, builder.Environment);
}
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.Cookie.Name = "AskRabbi.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.EventsType = typeof(WorkOsCookieAuthenticationEvents);
});
builder.Services.AddAuthorization();
if (localDevelopmentOptions.UseDemoServices)
{
    var localKeyDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), "AskARabbi", "LocalDevelopment-DataProtectionKeys"));
    builder.Services.AddDataProtection().PersistKeysToFileSystem(localKeyDirectory).SetApplicationName("AskARabbi.LocalDevelopment");
}

var app = builder.Build();

app.UseResponseCompression();
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}
app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseRouting();
app.UseCors(FrontendCorsOptions.PolicyName);
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapControllers();

app.Run();

/// <summary>Provides the application entry point to integration tests.</summary>
public partial class Program;
