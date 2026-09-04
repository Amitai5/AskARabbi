namespace AskARabbiLIB.DvarTorah.Audio;

/// <summary>Coordinates recoverable narration generation without changing the published article.</summary>
public sealed class WeeklyDvarTorahAudioCoordinator
{
    private readonly IWeeklyDvarTorahAudioStore store;
    private readonly IDvarTorahNarrator narrator;
    private readonly IDvarTorahAudioStorage storage;
    private readonly TimeProvider timeProvider;
    private readonly DvarTorahAudioOptions options;

    /// <summary>Initializes independent narration orchestration.</summary>
    /// <param name="store">Exclusive audio lease and metadata persistence.</param>
    /// <param name="narrator">Speech generation boundary.</param>
    /// <param name="storage">Private recording storage.</param>
    /// <param name="timeProvider">UTC time source.</param>
    /// <param name="options">Narration configuration.</param>
    public WeeklyDvarTorahAudioCoordinator(IWeeklyDvarTorahAudioStore store, IDvarTorahNarrator narrator, IDvarTorahAudioStorage storage, TimeProvider timeProvider, DvarTorahAudioOptions options)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.narrator = narrator ?? throw new ArgumentNullException(nameof(narrator));
        this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        if (options.Enabled)
        {
            options.ValidateGeneration();
        }
    }

    /// <summary>Generates or recovers the exact article version once, after text publication.</summary>
    /// <param name="article">Existing published article.</param>
    /// <param name="invocationId">Unique invocation identifier.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>The completed or safely deferred narration outcome.</returns>
    public async Task<WeeklyDvarTorahAudioResult> RunAsync(WeeklyDvarTorahArticle article, string invocationId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(article);
        ArgumentException.ThrowIfNullOrWhiteSpace(invocationId);
        if (invocationId.Length > 160)
        {
            throw new ArgumentException("The invocation identifier cannot exceed 160 characters.", nameof(invocationId));
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (!options.Enabled)
        {
            return new(WeeklyDvarTorahAudioStatus.Disabled, null);
        }

        var version = DvarTorahAudioText.GetVersion(article, options.Voice);
        if (article.Audio?.Version == version)
        {
            return new(WeeklyDvarTorahAudioStatus.AlreadyGenerated, article.Audio);
        }
        var now = timeProvider.GetUtcNow();
        var lease = await store.TryAcquireAudioLeaseAsync(article, version, invocationId, now, now.Add(options.LeaseDuration), cancellationToken).ConfigureAwait(false);
        if (lease is null)
        {
            return new(WeeklyDvarTorahAudioStatus.GenerationInProgress, null);
        }

        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(options.LeaseDuration - TimeSpan.FromMinutes(1));
            var audio = await storage.FindStoredAsync(article.Week.WeekKey, version, deadline.Token).ConfigureAwait(false);
            if (audio is not null)
            {
                var recovered = await storage.GetTimingsAsync(audio, deadline.Token).ConfigureAwait(false);
                if (recovered is null || recovered.Version != version || recovered.Title != DvarTorahAudioText.Normalize(article.Title) || recovered.Body != DvarTorahAudioText.Normalize(article.Body) || recovered.Voice != options.Voice)
                {
                    throw new InvalidDataException("The recovered recording does not match the published article and voice.");
                }
            }
            if (audio is null)
            {
                var narration = await narrator.GenerateAsync(article, version, deadline.Token).ConfigureAwait(false);
                DvarTorahAudioValidation.ValidateTimings(narration.Timings);
                if (narration.Timings.Version != version || narration.Timings.Title != DvarTorahAudioText.Normalize(article.Title) || narration.Timings.Body != DvarTorahAudioText.Normalize(article.Body) || narration.Timings.Voice != options.Voice)
                {
                    throw new InvalidDataException("The narration does not match the published article and configured voice.");
                }
                audio = await storage.UploadAsync(article.Week.WeekKey, narration, timeProvider.GetUtcNow(), deadline.Token).ConfigureAwait(false);
            }
            var published = await store.PublishAudioAsync(lease, article, audio, timeProvider.GetUtcNow(), deadline.Token).ConfigureAwait(false);
            return new(published ? WeeklyDvarTorahAudioStatus.Generated : WeeklyDvarTorahAudioStatus.LostLease, published ? audio : null);
        }
        catch (Exception exception)
        {
            // A canceled invocation releases only its own audio lease with a separate short cleanup budget.
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try
            {
                var failureCode = exception switch { DvarTorahAudioException audioException => audioException.FailureCode, OperationCanceledException => "Canceled", _ => exception.GetType().Name };
                await store.RecordAudioFailureAsync(lease, failureCode, timeProvider.GetUtcNow(), cleanup.Token).ConfigureAwait(false);
            }
            catch (Exception cleanupException)
            {
                throw new AggregateException("Narration failed and the audio lease could not be released; the text remains published.", exception, cleanupException);
            }
            throw;
        }
    }
}
