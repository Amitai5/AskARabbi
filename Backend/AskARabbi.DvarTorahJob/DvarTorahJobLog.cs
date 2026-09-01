using System.Text.Json;
using AskARabbiLIB.DvarTorah;

namespace AskARabbi.DvarTorahJob;

internal static class DvarTorahJobLog
{
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
        Console.Error.WriteLine(JsonSerializer.Serialize(new
        {
            timestampUtc = DateTimeOffset.UtcNow,
            level = "Error",
            eventName = "WeeklyDvarTorahGenerationFailed",
            failureCode = exception.GetType().Name,
            configurationError = exception is DvarTorahJobConfigurationException ? exception.Message : null,
        }));
    }
}
