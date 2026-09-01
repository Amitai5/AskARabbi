using AskARabbiLIB.DvarTorah;

namespace AskARabbi.DvarTorahJob;

internal sealed class DvarTorahJobApplication
{
    private readonly Func<bool> isGenerationEnabled;
    private readonly Func<WeeklyDvarTorahGenerationCoordinator> coordinatorFactory;
    private readonly Func<string> invocationIdFactory;

    internal DvarTorahJobApplication(Func<bool> isGenerationEnabled, Func<WeeklyDvarTorahGenerationCoordinator> coordinatorFactory, Func<string> invocationIdFactory)
    {
        this.isGenerationEnabled = isGenerationEnabled ?? throw new ArgumentNullException(nameof(isGenerationEnabled));
        this.coordinatorFactory = coordinatorFactory ?? throw new ArgumentNullException(nameof(coordinatorFactory));
        this.invocationIdFactory = invocationIdFactory ?? throw new ArgumentNullException(nameof(invocationIdFactory));
    }

    internal async Task<WeeklyDvarTorahGenerationResult?> RunAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!isGenerationEnabled())
        {
            return null;
        }

        var coordinator = coordinatorFactory() ?? throw new InvalidOperationException("The weekly Dvar Torah coordinator factory returned no coordinator.");
        var invocationId = invocationIdFactory();
        return await coordinator.RunAsync(invocationId, cancellationToken).ConfigureAwait(false);
    }
}
