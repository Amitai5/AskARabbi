using AskARabbiLIB.Persistence.Mongo;

namespace AskARabbi.Api.Persistence;

internal sealed class MongoIndexInitializer : IHostedService
{
    private readonly MongoIndexManager indexManager;
    private readonly ILogger<MongoIndexInitializer> logger;

    public MongoIndexInitializer(MongoIndexManager indexManager, ILogger<MongoIndexInitializer> logger)
    {
        this.indexManager = indexManager ?? throw new ArgumentNullException(nameof(indexManager));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await indexManager.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
        logger.LogInformation("MongoDB application indexes are ready.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
