using AskARabbi.Api.Authentication;
using AskARabbi.Api.Calendar;
using AskARabbiLIB.Calendar;
using AskARabbiLIB.Accounts;
using AskARabbiLIB.Conversations;
using AskARabbiLIB.ConversationSettings;
using AskARabbiLIB.DvarTorah;
using AskARabbiLIB.DvarTorah.Audio;
using AskARabbiLIB.Usage;
using AskARabbiLIB.Grounding;
using AskARabbiLIB.Retrieval;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace AskARabbi.Api.Tests;

internal sealed class TestApplicationFactory : WebApplicationFactory<Program>
{
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 8, 25, 12, 30, 0, TimeSpan.Zero);
    private readonly bool useApplicationFakes;
    private readonly string environmentName;
    private readonly bool useLocalDemoServices;
    private readonly string? corsOrigin;
    private readonly bool configureAi;

    internal TestApplicationFactory(bool useApplicationFakes = true, string environmentName = "Testing", bool useLocalDemoServices = false, string? corsOrigin = "https://frontend.askrabbi.test", bool configureAi = true)
    {
        this.useApplicationFakes = useApplicationFakes;
        this.environmentName = environmentName;
        this.useLocalDemoServices = useLocalDemoServices;
        this.corsOrigin = corsOrigin;
        this.configureAi = configureAi;
    }

    internal FakeUserAuthenticationService Authentication { get; } = new();

    internal InMemoryApplicationStore Store { get; init; } = new();

    internal int AccountLimit { get; init; } = 100;

    internal TimeProvider Clock { get; init; } = new FixedTimeProvider(FixedUtcNow);

    internal IDataProtectionProvider SessionKeys { get; init; } = new EphemeralDataProtectionProvider();

    internal FakeGroundedAnswerService GroundedAnswers { get; } = new();
    internal FakeCalendarProvider Calendar { get; } = new();

    internal InMemoryWeeklyDvarTorahStore WeeklyDvarTorah { get; } = new();

    internal FakeDvarTorahAudioReader DvarTorahAudio { get; } = new();

    internal bool IsAudioEnabled { get; set; } = true;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environmentName);
        builder.UseSetting("Registration:AccountLimit", AccountLimit.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.UseSetting("LocalDevelopment:UseDemoServices", useLocalDemoServices.ToString());
        if (configureAi)
        {
            builder.UseSetting("AI:ProjectEndpoint", "https://openai.askrabbi.test/");
            builder.UseSetting("AI:ModelName", "test-model");
            builder.UseSetting("AI:VectorStoreId", "vs_test");
            builder.UseSetting("AI:CorpusFingerprint", new string('a', 64));
            builder.UseSetting("AI:TenantId", "42c55b1a-363e-4e7a-b5f3-b6b275908185");
        }
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            var values = new Dictionary<string, string?>
            {
                ["WorkOS:ApiKey"] = string.Empty,
                ["WorkOS:ClientId"] = string.Empty,
                ["MongoDB:ConnectionString"] = string.Empty,
                ["MongoDB:DatabaseName"] = "askarabbi",
                ["LocalDevelopment:UseDemoServices"] = useLocalDemoServices.ToString(),
                ["Registration:AccountLimit"] = AccountLimit.ToString(System.Globalization.CultureInfo.InvariantCulture),
            };
            if (configureAi)
            {
                values["AI:ProjectEndpoint"] = "https://openai.askrabbi.test/";
                values["AI:ModelName"] = "test-model";
                values["AI:VectorStoreId"] = "vs_test";
                values["AI:CorpusFingerprint"] = new string('a', 64);
                values["AI:TenantId"] = "42c55b1a-363e-4e7a-b5f3-b6b275908185";
            }
            if (corsOrigin is not null)
            {
                values["Cors:AllowedOrigins:0"] = corsOrigin;
            }
            configuration.AddInMemoryCollection(values);
        });
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.RemoveAll<IHebcalCalendarClient>();
            services.AddSingleton<IHebcalCalendarClient>(Calendar);
            services.RemoveAll<IGroundedAnswerService>();
            services.AddSingleton<IGroundedAnswerService>(GroundedAnswers);
            services.RemoveAll<ISourceRetriever>();
            services.RemoveAll<ICanonicalSourceReader>();
            services.AddSingleton<ISourceRetriever, EmptyResearchCorpus>();
            services.AddSingleton<ICanonicalSourceReader, EmptyResearchCorpus>();

            if (useApplicationFakes)
            {
                services.RemoveAll<IUserAuthenticationService>();
                services.RemoveAll<IUserAccountStore>();
                services.RemoveAll<IAccountRegistrationStore>();
                services.RemoveAll<IUserDataStore>();
                services.RemoveAll<IConversationStore>();
                services.RemoveAll<IConversationSettingsStore>();
                services.RemoveAll<ICalendarPreferencesStore>();
                services.RemoveAll<IWeeklyDvarTorahReadStateStore>();
                services.RemoveAll<IUsageStore>();
                services.RemoveAll<IWeeklyDvarTorahStore>();
                services.RemoveAll<IDvarTorahAudioReader>();
                services.RemoveAll<DvarTorahAudioOptions>();

                services.AddSingleton<IUserAuthenticationService>(Authentication);
                services.AddSingleton<IUserAccountStore>(Store);
                services.AddSingleton<IAccountRegistrationStore>(new AskARabbiLIB.Persistence.InMemory.InMemoryAccountRegistrationStore(Store.GetAccountIdentities));
                services.AddSingleton<IUserDataStore>(Store);
                services.AddSingleton<IConversationStore>(Store);
                services.AddSingleton<IConversationSettingsStore>(Store);
                services.AddSingleton<ICalendarPreferencesStore>(Store);
                services.AddSingleton<IWeeklyDvarTorahReadStateStore>(Store);
                services.AddSingleton<IUsageStore>(Store);
                services.AddSingleton<IWeeklyDvarTorahStore>(WeeklyDvarTorah);
                services.AddSingleton<IDvarTorahAudioReader>(DvarTorahAudio);
                services.AddSingleton(new DvarTorahAudioOptions { Enabled = IsAudioEnabled });
            }

            services.AddSingleton(Clock);
            services.AddDataProtection();
            services.AddSingleton(SessionKeys);
        });
    }

    internal HttpClient CreateNonRedirectingClient() => CreateDefaultClient(new Uri("https://localhost"), new TestBrowserCookieHandler(Clock));

    internal async Task<HttpClient> CreateAuthenticatedClientAsync(Uri? baseAddress = null)
    {
        var client = CreateNonRedirectingClient();
        if (baseAddress is not null)
        {
            client.BaseAddress = baseAddress;
        }
        using var loginResponse = await client.GetAsync("/api/user/login");
        var state = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(loginResponse.Headers.Location!.Query)["state"].ToString();
        using var callbackResponse = await client.GetAsync($"/api/user/callback?code=test-code&state={Uri.EscapeDataString(state)}");
        if (callbackResponse.StatusCode != System.Net.HttpStatusCode.Redirect)
        {
            throw new InvalidOperationException($"Test login callback returned {(int)callbackResponse.StatusCode} instead of a redirect.");
        }

        return client;
    }

    private sealed class EmptyResearchCorpus : ISourceRetriever, ICanonicalSourceReader
    {
        public Task<IReadOnlyList<SourceRetrievalHit>> SearchAsync(SourceRetrievalQuery query, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SourceRetrievalHit>>([]);
        public Task<IReadOnlyList<SourceSegment>> GetContextAsync(string documentId, int documentOrdinal, int radius, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SourceSegment>>([]);
        public Task<IReadOnlyList<SourceSegment>> ReadAsync(string reference, SourceRetrievalQuery filters, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SourceSegment>>([]);
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset utcNow;

        internal FixedTimeProvider(DateTimeOffset utcNow)
        {
            this.utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
