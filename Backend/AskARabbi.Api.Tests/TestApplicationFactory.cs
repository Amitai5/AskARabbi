using AskARabbi.Api.Authentication;
using AskARabbiLIB.Accounts;
using AskARabbiLIB.Conversations;
using AskARabbiLIB.ConversationSettings;
using AskARabbiLIB.Usage;
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

    internal TestApplicationFactory(bool useApplicationFakes = true, string environmentName = "Testing", bool useLocalDemoServices = false, string? corsOrigin = "https://frontend.askrabbi.test")
    {
        this.useApplicationFakes = useApplicationFakes;
        this.environmentName = environmentName;
        this.useLocalDemoServices = useLocalDemoServices;
        this.corsOrigin = corsOrigin;
    }

    internal FakeUserAuthenticationService Authentication { get; } = new();

    internal InMemoryApplicationStore Store { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environmentName);
        builder.UseSetting("LocalDevelopment:UseDemoServices", useLocalDemoServices.ToString());
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            var values = new Dictionary<string, string?>
            {
                ["WorkOS:ApiKey"] = string.Empty,
                ["WorkOS:ClientId"] = string.Empty,
                ["MongoDB:ConnectionString"] = string.Empty,
                ["MongoDB:DatabaseName"] = "askarabbi",
                ["LocalDevelopment:UseDemoServices"] = useLocalDemoServices.ToString(),
            };
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

            if (useApplicationFakes)
            {
                services.RemoveAll<IUserAuthenticationService>();
                services.RemoveAll<IUserAccountStore>();
                services.RemoveAll<IConversationStore>();
                services.RemoveAll<IConversationSettingsStore>();
                services.RemoveAll<IUsageStore>();

                services.AddSingleton<IUserAuthenticationService>(Authentication);
                services.AddSingleton<IUserAccountStore>(Store);
                services.AddSingleton<IConversationStore>(Store);
                services.AddSingleton<IConversationSettingsStore>(Store);
                services.AddSingleton<IUsageStore>(Store);
            }

            services.AddSingleton<TimeProvider>(new FixedTimeProvider(FixedUtcNow));
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
        });
    }

    internal HttpClient CreateNonRedirectingClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = true,
        BaseAddress = new Uri("https://localhost"),
    });

    internal async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = CreateNonRedirectingClient();
        using var loginResponse = await client.GetAsync("/api/user/login");
        var state = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(loginResponse.Headers.Location!.Query)["state"].ToString();
        using var callbackResponse = await client.GetAsync($"/api/user/callback?code=test-code&state={Uri.EscapeDataString(state)}");
        if (callbackResponse.StatusCode != System.Net.HttpStatusCode.Redirect)
        {
            throw new InvalidOperationException($"Test login callback returned {(int)callbackResponse.StatusCode} instead of a redirect.");
        }

        return client;
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
