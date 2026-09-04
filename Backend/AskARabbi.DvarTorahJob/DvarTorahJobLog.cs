using System.Text.Json;
using AskARabbiLIB.CurrentEvents;
using AskARabbiLIB.DvarTorah;
using AskARabbiLIB.DvarTorah.Audio;

namespace AskARabbi.DvarTorahJob;

internal static class DvarTorahJobLog
{
    internal static void AudioCompleted(string weekKey, WeeklyDvarTorahAudioResult result)
    {
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            timestampUtc = DateTimeOffset.UtcNow,
            level = "Information",
            eventName = "WeeklyDvarTorahAudioCompleted",
            weekKey,
            status = result.Status.ToString(),
            version = result.Audio?.Version,
            durationMs = result.Audio?.DurationMs,
            audioLength = result.Audio?.AudioLength,
        }));
    }

    internal static void AudioFailed(string weekKey, Exception exception)
    {
        var diagnostic = DvarTorahAudioFailureDiagnostic.FromException(exception);
        Console.Error.WriteLine(JsonSerializer.Serialize(new
        {
            timestampUtc = DateTimeOffset.UtcNow,
            level = "Error",
            eventName = "WeeklyDvarTorahAudioFailed",
            weekKey,
            failureCode = diagnostic.FailureCode,
            stage = diagnostic.Stage,
            exceptionType = diagnostic.ExceptionType,
            innerExceptionType = diagnostic.InnerExceptionType,
            providerStatus = diagnostic.ProviderStatus,
            providerErrorCode = diagnostic.ProviderErrorCode,
            stackTrace = diagnostic.StackTrace,
            configurationError = exception is DvarTorahJobConfigurationException ? exception.Message : null,
            textRemainsPublished = true,
        }));
    }

    internal static void GenerationDisabled()
    {
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            timestampUtc = DateTimeOffset.UtcNow,
            level = "Information",
            eventName = "WeeklyDvarTorahGenerationDisabled",
        }));
    }

    internal static void GenerationCompleted(WeeklyDvarTorahGenerationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            timestampUtc = DateTimeOffset.UtcNow,
            level = "Information",
            eventName = "WeeklyDvarTorahGenerationCompleted",
            status = result.Status.ToString(),
            weekKey = result.Week.WeekKey,
            shabbatDate = result.Week.ShabbatDate,
            publishedAtUtc = result.Article?.PublishedAtUtc,
        }));
    }

    internal static void GenerationCanceled()
    {
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            timestampUtc = DateTimeOffset.UtcNow,
            level = "Warning",
            eventName = "WeeklyDvarTorahGenerationCanceled",
        }));
    }

    internal static void GenerationFailed(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var generationException = exception as WeeklyDvarTorahGenerationException;
        Console.Error.WriteLine(JsonSerializer.Serialize(new
        {
            timestampUtc = DateTimeOffset.UtcNow,
            level = "Error",
            eventName = "WeeklyDvarTorahGenerationFailed",
            failureCode = generationException?.FailureCode ?? exception.GetType().Name,
            diagnosticCategory = generationException?.DiagnosticCategory,
            failedChecks = generationException?.FailedChecks,
            responseId = generationException?.ProviderDiagnostics?.ResponseId,
            completionReason = generationException?.ProviderDiagnostics?.CompletionReason,
            providerAttempts = generationException?.ProviderDiagnostics?.Attempts,
            configurationError = exception is DvarTorahJobConfigurationException ? exception.Message : null,
        }));
    }

    internal static void NewsFeedFailed(FreeNewsFeed feed, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(feed);
        ArgumentNullException.ThrowIfNull(exception);
        Console.Error.WriteLine(JsonSerializer.Serialize(new
        {
            timestampUtc = DateTimeOffset.UtcNow,
            level = "Warning",
            eventName = "WeeklyDvarTorahNewsFeedFailed",
            publisher = feed.Publisher,
            feedHost = feed.FeedUrl.Host,
            failureCode = exception.GetType().Name,
        }));
    }
}
