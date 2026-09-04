using AskARabbiLIB.DvarTorah;
using AskARabbiLIB.DvarTorah.Audio;

namespace AskARabbiLIB.Tests;

internal static class DvarTorahAudioTestData
{
    internal static readonly DateTimeOffset Now = new(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);
    internal const string Voice = "en-US-AndrewMultilingualNeural";

    internal static WeeklyDvarTorahArticle Article(string body = "Hello world.") => new(new WeeklyDvarTorahWeek(new DateOnly(2026, 9, 5), "23 Elul 5786", "Nitzavim", null, false), "Title", body, "test-v1", Now, Now);

    internal static DvarTorahAudioOptions Options(bool enabled = true) => new()
    {
        Enabled = enabled,
        StorageServiceUri = "https://testaccount.blob.core.windows.net/",
        SpeechResourceId = "/subscriptions/11111111-1111-1111-1111-111111111111/resourceGroups/test/providers/Microsoft.CognitiveServices/accounts/test-speech",
    };

    internal static DvarTorahAudioTimings Timings() => new()
    {
        Version = DvarTorahAudioText.GetVersion(Article(), Voice), Voice = Voice, Title = "Title", Body = "Hello world.", DurationMs = 1500,
        Words = [new("title", "Title", 0, 5, 0, 500), new("body", "Hello", 0, 5, 600, 400), new("body", "world", 6, 5, 1000, 400)],
    };

    internal static WeeklyDvarTorahAudioMetadata Metadata() => new()
    {
        Version = Timings().Version, Voice = Voice, DurationMs = 1500, AudioLength = 4, CreatedAtUtc = Now,
        BlobName = $"diaspora/2026-09-05/{Timings().Version}/{new string('b', 64)}/narration.mp3",
        BlobUri = $"https://testaccount.blob.core.windows.net/dvar-torah-audio/diaspora/2026-09-05/{Timings().Version}/{new string('b', 64)}/narration.mp3",
        TimingsBlobName = $"diaspora/2026-09-05/{Timings().Version}/{new string('b', 64)}/timings.json",
    };
}
