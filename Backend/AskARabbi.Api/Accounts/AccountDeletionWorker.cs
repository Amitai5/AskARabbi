using AskARabbiLIB.Accounts;

namespace AskARabbi.Api.Accounts;

internal sealed class AccountDeletionWorker(IServiceScopeFactory scopes, ILogger<AccountDeletionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                var data = scope.ServiceProvider.GetRequiredService<IUserDataStore>();
                var service = scope.ServiceProvider.GetRequiredService<AccountDeletionService>();
                foreach (var deletion in await data.ListPendingDeletionsAsync(stoppingToken).ConfigureAwait(false))
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                    timeout.CancelAfter(TimeSpan.FromSeconds(30));
                    try
                    {
                        await service.TryCompleteAsync(deletion, timeout.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested && timeout.IsCancellationRequested)
                    {
                        logger.LogWarning("Account erasure for {UserId} exceeded its attempt deadline; keeping it pending and continuing with other accounts.", deletion.UserId);
                    }
                }
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(exception, "Pending account erasures could not be processed; retrying on the next cycle.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }
}
