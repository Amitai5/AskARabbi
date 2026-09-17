using System.Threading.RateLimiting;
using Azure.Core;
using Azure.Identity;
using Microsoft.AspNetCore.RateLimiting;

namespace AskARabbi.Api.Voice;

internal static class VoiceServiceCollectionExtensions
{
    internal static IServiceCollection AddConversationVoice(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var options = configuration.GetSection(VoiceOptions.SectionName).Get<VoiceOptions>() ?? new VoiceOptions();
        options.Validate();
        services.AddSingleton(options);
        services.AddHttpClient("ConversationVoice", client => client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddSingleton<IVoiceService>(provider =>
        {
            TokenCredential credential = environment.IsDevelopment() ? new DefaultAzureCredential() : new ManagedIdentityCredential(ManagedIdentityId.SystemAssigned);
            return new AzureVoiceService(options, credential, provider.GetRequiredService<IHttpClientFactory>().CreateClient("ConversationVoice"));
        });
        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.AddPolicy("conversation-voice", context => RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "anonymous",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });
        return services;
    }
}
