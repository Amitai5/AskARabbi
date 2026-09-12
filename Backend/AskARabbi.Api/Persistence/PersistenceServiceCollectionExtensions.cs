using AskARabbiLIB.Accounts;
using AskARabbiLIB.Calendar;
using AskARabbiLIB.Conversations;
using AskARabbiLIB.ConversationSettings;
using AskARabbiLIB.DvarTorah;
using AskARabbiLIB.Lexicon;
using AskARabbiLIB.Persistence.Mongo;
using AskARabbiLIB.Usage;
using MongoDB.Driver;

namespace AskARabbi.Api.Persistence;

internal static class PersistenceServiceCollectionExtensions
{
    internal static IServiceCollection AddAskRabbiPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(MongoDatabaseOptions.SectionName).Get<MongoDatabaseOptions>() ?? new MongoDatabaseOptions();
        services.AddSingleton(options);

        if (!options.IsConfigured)
        {
            services.AddSingleton<ILexiconStore, UnavailableLexiconStore>();
            services.AddSingleton<UnavailableApplicationStore>();
            services.AddSingleton<IUserAccountStore>(provider => provider.GetRequiredService<UnavailableApplicationStore>());
            services.AddSingleton<IAccountRegistrationStore>(provider => provider.GetRequiredService<UnavailableApplicationStore>());
            services.AddSingleton<IUserDataStore>(provider => provider.GetRequiredService<UnavailableApplicationStore>());
            services.AddSingleton<IConversationStore>(provider => provider.GetRequiredService<UnavailableApplicationStore>());
            services.AddSingleton<IConversationSettingsStore>(provider => provider.GetRequiredService<UnavailableApplicationStore>());
            services.AddSingleton<ICalendarPreferencesStore>(provider => provider.GetRequiredService<UnavailableApplicationStore>());
            services.AddSingleton<IWeeklyDvarTorahReadStateStore>(provider => provider.GetRequiredService<UnavailableApplicationStore>());
            services.AddSingleton<IUsageStore>(provider => provider.GetRequiredService<UnavailableApplicationStore>());
            services.AddSingleton<IWeeklyDvarTorahStore>(provider => provider.GetRequiredService<UnavailableApplicationStore>());
            return services;
        }

        options.Validate();
        services.AddSingleton<IMongoClient>(_ => new MongoClient(MongoClientSettings.FromConnectionString(options.ConnectionString)));
        services.AddSingleton(provider => provider.GetRequiredService<IMongoClient>().GetDatabase(options.DatabaseName));
        services.AddSingleton<ILexiconStore, MongoLexiconStore>();
        services.AddSingleton<IUserAccountStore, MongoUserAccountStore>();
        services.AddSingleton<IAccountRegistrationStore, MongoAccountRegistrationStore>();
        services.AddSingleton<IUserDataStore, MongoUserDataStore>();
        services.AddHostedService<AskARabbi.Api.Accounts.AccountDeletionWorker>();
        services.AddSingleton<IConversationStore, MongoConversationStore>();
        services.AddSingleton<MongoConversationSettingsStore>();
        services.AddSingleton<IConversationSettingsStore>(provider => provider.GetRequiredService<MongoConversationSettingsStore>());
        services.AddSingleton<ICalendarPreferencesStore>(provider => provider.GetRequiredService<MongoConversationSettingsStore>());
        services.AddSingleton<IWeeklyDvarTorahReadStateStore>(provider => provider.GetRequiredService<MongoConversationSettingsStore>());
        services.AddSingleton<IUsageStore, MongoUsageStore>();
        services.AddSingleton<MongoWeeklyDvarTorahStore>();
        services.AddSingleton<IWeeklyDvarTorahStore>(provider => provider.GetRequiredService<MongoWeeklyDvarTorahStore>());
        services.AddSingleton<MongoIndexManager>();
        services.AddHostedService<MongoIndexInitializer>();
        return services;
    }
}
