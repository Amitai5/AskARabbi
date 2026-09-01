namespace AskARabbiLIB.DvarTorah;

/// <summary>Coordinates idempotent weekly generation with a recoverable exclusive lease.</summary>
public sealed class WeeklyDvarTorahGenerationCoordinator
{
    private readonly IWeeklyDvarTorahGenerationStore store;
    private readonly IWeeklyDvarTorahGenerator generator;
    private readonly WeeklyDvarTorahService weeklyService;
    private readonly TimeProvider timeProvider;
    private readonly WeeklyDvarTorahOptions options;

    /// <summary>Initializes the weekly generation coordinator.</summary>
    /// <param name="store">Generation and publication store.</param>
    /// <param name="generator">Configured content generator.</param>
    /// <param name="weeklyService">Current-week resolver.</param>
    /// <param name="timeProvider">Current-time provider.</param>
    /// <param name="options">Lease configuration.</param>
    public WeeklyDvarTorahGenerationCoordinator(IWeeklyDvarTorahGenerationStore store, IWeeklyDvarTorahGenerator generator, WeeklyDvarTorahService weeklyService, TimeProvider timeProvider, WeeklyDvarTorahOptions options)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.generator = generator ?? throw new ArgumentNullException(nameof(generator));
        this.weeklyService = weeklyService ?? throw new ArgumentNullException(nameof(weeklyService));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        options.Validate();
    }

    /// <summary>Generates and publishes the current week exactly once across concurrent or retried invocations.</summary>
    /// <param name="invocationId">Unique scheduler invocation identifier.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>The idempotent invocation outcome.</returns>
    public async Task<WeeklyDvarTorahGenerationResult> RunAsync(string invocationId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(invocationId))
        {
            throw new ArgumentException("A scheduler invocation ID is required.", nameof(invocationId));
        }

        var normalizedInvocationId = invocationId.Trim();
        if (normalizedInvocationId.Length > 160)
        {
            throw new ArgumentException("The scheduler invocation ID cannot exceed 160 characters.", nameof(invocationId));
        }

        var week = weeklyService.GetCurrentWeek();
        var existing = await store.GetPublishedAsync(week, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return new WeeklyDvarTorahGenerationResult(WeeklyDvarTorahGenerationStatus.AlreadyPublished, week, existing);
        }

        var acquiredAtUtc = timeProvider.GetUtcNow();
        var expiresAtUtc = acquiredAtUtc.Add(options.GenerationLeaseDuration);
        var lease = await store.TryAcquireGenerationLeaseAsync(week, normalizedInvocationId, acquiredAtUtc, expiresAtUtc, cancellationToken).ConfigureAwait(false);
        if (lease is null)
        {
            existing = await store.GetPublishedAsync(week, cancellationToken).ConfigureAwait(false);
            return existing is null
                ? new WeeklyDvarTorahGenerationResult(WeeklyDvarTorahGenerationStatus.GenerationInProgress, week, null)
                : new WeeklyDvarTorahGenerationResult(WeeklyDvarTorahGenerationStatus.AlreadyPublished, week, existing);
        }

        try
        {
            var draft = await generator.GenerateAsync(week, cancellationToken).ConfigureAwait(false);
            ArgumentNullException.ThrowIfNull(draft);
            var completedAtUtc = timeProvider.GetUtcNow();
            var article = new WeeklyDvarTorahArticle(week, draft.Title, draft.Body, draft.GeneratorVersion, completedAtUtc, completedAtUtc);
            if (!await store.PublishAsync(lease, article, cancellationToken).ConfigureAwait(false))
            {
                throw new InvalidOperationException("The weekly Dvar Torah generation lease expired before publication completed.");
            }

            return new WeeklyDvarTorahGenerationResult(WeeklyDvarTorahGenerationStatus.Published, week, article);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            try
            {
                await store.RecordGenerationFailureAsync(lease, exception.GetType().Name, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception persistenceException)
            {
                throw new AggregateException("Weekly Dvar Torah generation failed and its lease could not be released.", exception, persistenceException);
            }

            throw;
        }
    }
}
