using AskARabbi.Api.Authentication;
using AskARabbiLIB.Accounts;
using AskARabbiLIB.Calendar;
using AskARabbiLIB.Conversations;
using AskARabbiLIB.ConversationSettings;
using AskARabbiLIB.DvarTorah;
using AskARabbiLIB.Lexicon;
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
        services.AddSingleton<ILexiconStore, UnavailableLexiconStore>();
        services.AddSingleton<IUserAccountStore>(provider => provider.GetRequiredService<LocalDevelopmentApplicationStore>());
        services.AddSingleton<IAccountRegistrationStore>(provider => new AskARabbiLIB.Persistence.InMemory.InMemoryAccountRegistrationStore(provider.GetRequiredService<LocalDevelopmentApplicationStore>().GetAccountIdentities));
        services.AddSingleton<IUserDataStore>(provider => provider.GetRequiredService<LocalDevelopmentApplicationStore>());
        services.AddHostedService<AskARabbi.Api.Accounts.AccountDeletionWorker>();
        services.AddSingleton<IConversationStore>(provider => provider.GetRequiredService<LocalDevelopmentApplicationStore>());
        services.AddSingleton<IConversationSettingsStore>(provider => provider.GetRequiredService<LocalDevelopmentApplicationStore>());
        services.AddSingleton<ICalendarPreferencesStore>(provider => provider.GetRequiredService<LocalDevelopmentApplicationStore>());
        services.AddSingleton<IWeeklyDvarTorahReadStateStore>(provider => provider.GetRequiredService<LocalDevelopmentApplicationStore>());
        services.AddSingleton<IUsageStore>(provider => provider.GetRequiredService<LocalDevelopmentApplicationStore>());
        services.AddSingleton<IWeeklyDvarTorahStore>(provider => provider.GetRequiredService<LocalDevelopmentApplicationStore>());
        services.AddSingleton<IUserAuthenticationService, LocalDevelopmentAuthenticationService>();
        return services;
    }
}
