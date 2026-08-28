using AskARabbiLIB.Accounts;
using AskARabbiLIB.Conversations;
using AskARabbiLIB.ConversationSettings;
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
            services.AddSingleton<UnavailableApplicationStore>();
            services.AddSingleton<IUserAccountStore>(provider => provider.GetRequiredService<UnavailableApplicationStore>());
            services.AddSingleton<IConversationStore>(provider => provider.GetRequiredService<UnavailableApplicationStore>());
            services.AddSingleton<IConversationSettingsStore>(provider => provider.GetRequiredService<UnavailableApplicationStore>());
            services.AddSingleton<IUsageStore>(provider => provider.GetRequiredService<UnavailableApplicationStore>());
            return services;
        }

        options.Validate();
        services.AddSingleton<IMongoClient>(_ => new MongoClient(MongoClientSettings.FromConnectionString(options.ConnectionString)));
        services.AddSingleton(provider => provider.GetRequiredService<IMongoClient>().GetDatabase(options.DatabaseName));
        services.AddSingleton<IUserAccountStore, MongoUserAccountStore>();
        services.AddSingleton<IConversationStore, MongoConversationStore>();
        services.AddSingleton<IConversationSettingsStore, MongoConversationSettingsStore>();
        services.AddSingleton<IUsageStore, MongoUsageStore>();
        services.AddSingleton<MongoIndexManager>();
        services.AddHostedService<MongoIndexInitializer>();
        return services;
    }
}
