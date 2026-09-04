using System.Globalization;
using AskARabbiLIB.DvarTorah;
using AskARabbiLIB.DvarTorah.Audio;

namespace AskARabbi.DvarTorahJob;

internal sealed class DvarTorahJobApplication
{
    private readonly Func<bool> isGenerationEnabled;
    private readonly Func<string, CancellationToken, Task<WeeklyDvarTorahGenerationResult>> generateAsync;
    private readonly Func<string> invocationIdFactory;
    private readonly Func<string?> audioOnlyWeekKey;
    private readonly Func<string, CancellationToken, Task<WeeklyDvarTorahArticle?>> loadPublishedAsync;
    private readonly Func<WeeklyDvarTorahArticle, string, CancellationToken, Task<WeeklyDvarTorahAudioResult>> generateAudioAsync;

    internal DvarTorahJobApplication(Func<bool> isGenerationEnabled, Func<string, CancellationToken, Task<WeeklyDvarTorahGenerationResult>> generateAsync, Func<string> invocationIdFactory, Func<string?> audioOnlyWeekKey, Func<string, CancellationToken, Task<WeeklyDvarTorahArticle?>> loadPublishedAsync, Func<WeeklyDvarTorahArticle, string, CancellationToken, Task<WeeklyDvarTorahAudioResult>> generateAudioAsync)
    {
        this.isGenerationEnabled = isGenerationEnabled ?? throw new ArgumentNullException(nameof(isGenerationEnabled));
        this.generateAsync = generateAsync ?? throw new ArgumentNullException(nameof(generateAsync));
        this.invocationIdFactory = invocationIdFactory ?? throw new ArgumentNullException(nameof(invocationIdFactory));
        this.audioOnlyWeekKey = audioOnlyWeekKey ?? throw new ArgumentNullException(nameof(audioOnlyWeekKey));
        this.loadPublishedAsync = loadPublishedAsync ?? throw new ArgumentNullException(nameof(loadPublishedAsync));
        this.generateAudioAsync = generateAudioAsync ?? throw new ArgumentNullException(nameof(generateAudioAsync));
    }

    internal async Task<DvarTorahJobResult?> RunAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var backfillWeekKey = audioOnlyWeekKey();
        if (backfillWeekKey is null && !isGenerationEnabled())
        {
            return null;
        }

        var invocationId = invocationIdFactory();
        WeeklyDvarTorahGenerationResult generation;
        if (backfillWeekKey is not null)
        {
            ValidateBackfillWeekKey(backfillWeekKey);
            var article = await loadPublishedAsync(backfillWeekKey, cancellationToken).ConfigureAwait(false)
                ?? throw new DvarTorahJobConfigurationException("The requested audio backfill week has no published Dvar Torah.");
            generation = new WeeklyDvarTorahGenerationResult(WeeklyDvarTorahGenerationStatus.AlreadyPublished, article.Week, article);
        }
        else
        {
            generation = await generateAsync(invocationId, cancellationToken).ConfigureAwait(false);
        }

        if (generation.Article is null)
        {
            return new DvarTorahJobResult(generation, null, null);
        }

        try
        {
            var audio = await generateAudioAsync(generation.Article, invocationId, cancellationToken).ConfigureAwait(false);
            if (backfillWeekKey is not null && audio.Status == WeeklyDvarTorahAudioStatus.Disabled)
            {
                throw new DvarTorahJobConfigurationException("DvarTorahAudio__Enabled must be true for an audio-only backfill.");
            }

            return new DvarTorahJobResult(generation, audio, audio.Status == WeeklyDvarTorahAudioStatus.LostLease ? "AudioLeaseLost" : null);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // Text is already durably published. A failed recording requests a safe job retry,
            // which reuses the article and the audio coordinator's independent idempotency lease.
            DvarTorahJobLog.AudioFailed(generation.Week.WeekKey, exception);
            return new DvarTorahJobResult(generation, null, DvarTorahAudioFailureDiagnostic.FromException(exception).FailureCode);
        }
    }

    private static void ValidateBackfillWeekKey(string weekKey)
    {
        var prefix = weekKey.StartsWith("diaspora:", StringComparison.Ordinal) ? "diaspora:" : "israel:";
        if (!weekKey.StartsWith(prefix, StringComparison.Ordinal) || weekKey.Length != prefix.Length + 10
            || !DateOnly.TryParseExact(weekKey.AsSpan(prefix.Length), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            || date.DayOfWeek != DayOfWeek.Saturday)
        {
            throw new DvarTorahJobConfigurationException("DvarTorahAudio__BackfillWeekKey must be a published diaspora:yyyy-MM-dd or israel:yyyy-MM-dd Shabbat key.");
        }
    }
}
