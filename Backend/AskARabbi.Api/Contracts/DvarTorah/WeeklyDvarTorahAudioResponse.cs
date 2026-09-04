namespace AskARabbi.Api.Contracts.DvarTorah;

/// <summary>Describes a ready recording without exposing its private Blob Storage address.</summary>
/// <param name="Version">Immutable recording version used to prevent stale playback and timing data.</param>
/// <param name="Voice">Azure Speech voice used to narrate the article.</param>
/// <param name="DurationMs">Recording duration in milliseconds.</param>
/// <param name="AudioUrl">Authenticated, relative API URL for the MP3 recording.</param>
/// <param name="TimingsUrl">Authenticated, relative API URL for word timing data.</param>
public sealed record WeeklyDvarTorahAudioResponse(string Version, string Voice, double DurationMs, string AudioUrl, string TimingsUrl);
