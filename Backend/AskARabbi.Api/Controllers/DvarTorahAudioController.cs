using System.Globalization;
using System.Text.Json;
using AskARabbi.Api.DvarTorahAudio;
using AskARabbiLIB.DvarTorah;
using AskARabbiLIB.DvarTorah.Audio;
using Azure;
using Azure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AskARabbi.Api.Controllers;

/// <summary>Streams ready private narration and its timing manifest through the authenticated API.</summary>
[ApiController]
[Authorize]
[Route("api/dvar-torah/archive/{weekKey}/audio")]
public sealed class DvarTorahAudioController : ControllerBase
{
    private readonly IWeeklyDvarTorahStore articles;
    private readonly IDvarTorahAudioReader audioReader;
    private readonly WeeklyDvarTorahOptions weeklyOptions;
    private readonly ILogger<DvarTorahAudioController> logger;

    /// <summary>Initializes authenticated access to privately stored recordings.</summary>
    /// <param name="articles">Published-article store.</param>
    /// <param name="audioReader">Private storage reader with a fixed trusted account and container.</param>
    /// <param name="weeklyOptions">Configured reading cycle.</param>
    /// <param name="logger">Structured boundary logger.</param>
    public DvarTorahAudioController(IWeeklyDvarTorahStore articles, IDvarTorahAudioReader audioReader, WeeklyDvarTorahOptions weeklyOptions, ILogger<DvarTorahAudioController> logger)
    {
        this.articles = articles ?? throw new ArgumentNullException(nameof(articles));
        this.audioReader = audioReader ?? throw new ArgumentNullException(nameof(audioReader));
        this.weeklyOptions = weeklyOptions ?? throw new ArgumentNullException(nameof(weeklyOptions));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Streams the current or an archived publication's MP3 with bounded byte-range support.</summary>
    /// <param name="weekKey">Published reading-cycle and Shabbat key.</param>
    /// <param name="version">Optional immutable version expected by the player's timing data.</param>
    /// <param name="download">Whether to return an attachment filename for an explicit download.</param>
    /// <param name="cancellationToken">Token that cancels database and storage IO.</param>
    /// <returns>The MP3, its headers, or a safe unavailable response.</returns>
    [HttpGet]
    [HttpHead]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status206PartialContent)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status416RangeNotSatisfiable)]
    public async Task<IActionResult> GetAudio(string weekKey, [FromQuery] string? version, [FromQuery] bool download = false, CancellationToken cancellationToken = default)
    {
        var audio = await GetPublishedAudioAsync(weekKey, cancellationToken).ConfigureAwait(false);
        if (audio is null)
        {
            return NotFound();
        }
        if (version is not null && !string.Equals(version, audio.Version, StringComparison.Ordinal))
        {
            return StaleRecording();
        }

        try
        {
            var info = await audioReader.GetInfoAsync(audio, cancellationToken).ConfigureAwait(false);
            if (info is null)
            {
                return NotFound();
            }

            AudioHttpCache.SetHeaders(Response, info.ETag, info.LastModified, version is not null);
            Response.Headers.AcceptRanges = "bytes";
            if (AudioHttpCache.IsNotModified(Request, info.ETag, info.LastModified))
            {
                return StatusCode(StatusCodes.Status304NotModified);
            }

            var isHead = HttpMethods.IsHead(Request.Method);
            var rangeHeader = !isHead && AudioHttpCache.AllowsRange(Request, info.ETag, info.LastModified) ? Request.Headers.Range.ToString() : null;
            if (!AudioByteRange.TryCreate(rangeHeader, info.Length, out var range))
            {
                Response.Headers.ContentRange = $"bytes */{info.Length.ToString(CultureInfo.InvariantCulture)}";
                return StatusCode(StatusCodes.Status416RangeNotSatisfiable);
            }

            Response.StatusCode = range.IsPartial ? StatusCodes.Status206PartialContent : StatusCodes.Status200OK;
            Response.ContentType = "audio/mpeg";
            Response.ContentLength = range.Length;
            if (download)
            {
                Response.Headers.ContentDisposition = $"attachment; filename=\"askarabbi-dvar-torah-{weekKey.Replace(':', '-')}.mp3\"";
            }
            if (range.IsPartial)
            {
                Response.Headers.ContentRange = FormattableString.Invariant($"bytes {range.Offset}-{range.Offset + range.Length - 1}/{info.Length}");
            }
            if (isHead)
            {
                return new EmptyResult();
            }

            // MVC owns this network stream and disposes it after writing or request cancellation.
            var stream = await audioReader.OpenReadAsync(audio, range.Offset, range.Length, cancellationToken).ConfigureAwait(false);
            return File(stream, "audio/mpeg", enableRangeProcessing: false);
        }
        catch (RequestFailedException exception) when (exception.Status == StatusCodes.Status404NotFound)
        {
            ClearAudioHeaders();
            return NotFound();
        }
        catch (Exception exception) when (IsAudioBoundaryFailure(exception))
        {
            return AudioUnavailable(weekKey, exception);
        }
    }

    /// <summary>Loads word offsets tied to the ready recording's exact display text.</summary>
    /// <param name="weekKey">Published reading-cycle and Shabbat key.</param>
    /// <param name="version">Optional immutable version expected by the player.</param>
    /// <param name="cancellationToken">Token that cancels database and storage IO.</param>
    /// <returns>The validated timing manifest or a safe unavailable response.</returns>
    [HttpGet("timings")]
    [ProducesResponseType<DvarTorahAudioTimings>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GetTimings(string weekKey, [FromQuery] string? version, CancellationToken cancellationToken)
    {
        var audio = await GetPublishedAudioAsync(weekKey, cancellationToken).ConfigureAwait(false);
        if (audio is null)
        {
            return NotFound();
        }
        if (version is not null && !string.Equals(version, audio.Version, StringComparison.Ordinal))
        {
            return StaleRecording();
        }

        try
        {
            var timings = await audioReader.GetTimingsAsync(audio, cancellationToken).ConfigureAwait(false);
            if (timings is null)
            {
                return NotFound();
            }

            var entityTag = $"\"{audio.Version}-timings\"";
            AudioHttpCache.SetHeaders(Response, entityTag, audio.CreatedAtUtc, version is not null);
            return AudioHttpCache.IsNotModified(Request, entityTag, audio.CreatedAtUtc)
                ? StatusCode(StatusCodes.Status304NotModified)
                : Ok(timings);
        }
        catch (Exception exception) when (IsAudioBoundaryFailure(exception))
        {
            return AudioUnavailable(weekKey, exception);
        }
    }

    private async Task<WeeklyDvarTorahAudioMetadata?> GetPublishedAudioAsync(string weekKey, CancellationToken cancellationToken)
    {
        var prefix = weeklyOptions.InIsrael ? "israel:" : "diaspora:";
        if (weekKey.Length != prefix.Length + 10 || !weekKey.StartsWith(prefix, StringComparison.Ordinal)
            || !DateOnly.TryParseExact(weekKey.AsSpan(prefix.Length), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            || date.DayOfWeek != DayOfWeek.Saturday)
        {
            return null;
        }

        var article = await articles.GetPublishedByWeekKeyAsync(weekKey, cancellationToken).ConfigureAwait(false);
        return article?.Audio;
    }

    private ConflictObjectResult StaleRecording() => Conflict(new ProblemDetails { Status = StatusCodes.Status409Conflict, Title = "Recording changed", Detail = "Reload the Dvar Torah to play its current recording." });

    private ObjectResult AudioUnavailable(string weekKey, Exception exception)
    {
        logger.LogWarning(exception, "Dvar Torah audio storage is unavailable for {WeekKey}.", weekKey);
        ClearAudioHeaders();
        Response.Headers.RetryAfter = "30";
        return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails { Status = StatusCodes.Status503ServiceUnavailable, Title = "Recording temporarily unavailable", Detail = "The text is still available. Please try the recording again shortly." });
    }

    private void ClearAudioHeaders()
    {
        Response.ContentLength = null;
        Response.ContentType = null;
        Response.Headers.Remove("Content-Range");
        Response.Headers.Remove("Content-Disposition");
        Response.Headers.Remove("ETag");
        Response.Headers.Remove("Last-Modified");
        Response.Headers.CacheControl = "no-store";
    }

    private static bool IsAudioBoundaryFailure(Exception exception) => exception is RequestFailedException or AuthenticationFailedException or IOException or JsonException or InvalidOperationException or ArgumentException or TimeoutException;
}
