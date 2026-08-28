using AskARabbi.Api.Authentication;
using AskARabbiLIB.Accounts;
using AskARabbiLIB.Conversations;
using AskARabbiLIB.ConversationSettings;
using AskARabbiLIB.Usage;

namespace AskARabbi.Api.Development;

internal static class LocalDevelopmentServiceCollectionExtensions
{
    internal static IServiceCollection AddAskRabbiLocalDevelopment(this IServiceCollection services, IConfiguration configuration)
    {
        var authenticationOptions = configuration.GetSection(WorkOsAuthenticationOptions.SectionName).Get<WorkOsAuthenticationOptions>() ?? new WorkOsAuthenticationOptions();
        authenticationOptions.ValidateRedirectUris();

        services.AddSingleton(authenticationOptions);
        services.AddSingleton<LocalDevelopmentApplicationStore>();
        services.AddSingleton<IUserAccountStore>(provider => provider.GetRequiredService<LocalDevelopmentApplicationStore>());
        services.AddSingleton<IConversationStore>(provider => provider.GetRequiredService<LocalDevelopmentApplicationStore>());
        services.AddSingleton<IConversationSettingsStore>(provider => provider.GetRequiredService<LocalDevelopmentApplicationStore>());
        services.AddSingleton<IUsageStore>(provider => provider.GetRequiredService<LocalDevelopmentApplicationStore>());
        services.AddSingleton<IUserAuthenticationService, LocalDevelopmentAuthenticationService>();
        return services;
    }
}
