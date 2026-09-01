using AskARabbiLIB.Calendar;
using AskARabbiLIB.DvarTorah;
using AskARabbiLIB.Persistence.Mongo;
using MongoDB.Driver;

namespace AskARabbi.DvarTorahJob;

internal static class JobDependencyFactory
{
    internal static WeeklyDvarTorahGenerationCoordinator CreateCoordinator()
    {
        var databaseOptions = new MongoDatabaseOptions
        {
            ConnectionString = DvarTorahJobEnvironment.GetRequired("MongoDB__ConnectionString"),
            DatabaseName = DvarTorahJobEnvironment.GetOptional("MongoDB__DatabaseName") ?? "askarabbi",
            DvarTorahCollectionName = DvarTorahJobEnvironment.GetOptional("MongoDB__DvarTorahCollectionName") ?? "WeeklyAIDvarTorahs",
        };
        databaseOptions.Validate();

        var dvarTorahOptions = new WeeklyDvarTorahOptions
        {
            InIsrael = DvarTorahJobEnvironment.GetBoolean("DvarTorah__InIsrael", false),
            GenerationLeaseMinutes = DvarTorahJobEnvironment.GetInteger("DvarTorah__GenerationLeaseMinutes", 30),
        };
        dvarTorahOptions.Validate();

        var mongoClient = new MongoClient(MongoClientSettings.FromConnectionString(databaseOptions.ConnectionString));
        var database = mongoClient.GetDatabase(databaseOptions.DatabaseName);
        var store = new MongoWeeklyDvarTorahStore(database, databaseOptions);
        var timeProvider = TimeProvider.System;
        var weeklyService = new WeeklyDvarTorahService(new HebrewCalendarService(), store, timeProvider, dvarTorahOptions);
        return new WeeklyDvarTorahGenerationCoordinator(store, new UnconfiguredWeeklyDvarTorahGenerator(), weeklyService, timeProvider, dvarTorahOptions);
    }
}
