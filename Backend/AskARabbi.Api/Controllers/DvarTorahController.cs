using AskARabbi.Api.Contracts.DvarTorah;
using AskARabbi.Api.Authentication;
using AskARabbiLIB.DvarTorah;
using AskARabbiLIB.DvarTorah.Audio;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AskARabbi.Api.Controllers;

/// <summary>Loads current and archived published weekly Dvar Torahs.</summary>
[ApiController]
[Authorize]
[Route("api/dvar-torah")]
public sealed class DvarTorahController : ControllerBase
{
    private readonly WeeklyDvarTorahService weeklyDvarTorah;
    private readonly DvarTorahAudioOptions audioOptions;
    private readonly IWeeklyDvarTorahReadStateStore readState;
    private readonly ICurrentUser currentUser;
    private readonly TimeProvider clock;

    /// <summary>Initializes the weekly Dvar Torah API.</summary>
    /// <param name="weeklyDvarTorah">Weekly publication service.</param>
    /// <param name="audioOptions">Private audio availability configuration.</param>
    /// <param name="readState">Account-owned teaching progress.</param>
    /// <param name="currentUser">Authenticated account accessor.</param>
    /// <param name="clock">Clock for progress updates.</param>
    public DvarTorahController(WeeklyDvarTorahService weeklyDvarTorah, DvarTorahAudioOptions audioOptions, IWeeklyDvarTorahReadStateStore readState, ICurrentUser currentUser, TimeProvider clock)
    {
        this.weeklyDvarTorah = weeklyDvarTorah ?? throw new ArgumentNullException(nameof(weeklyDvarTorah));
        this.audioOptions = audioOptions ?? throw new ArgumentNullException(nameof(audioOptions));
        this.readState = readState ?? throw new ArgumentNullException(nameof(readState));
        this.currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>Gets this week's publication or the latest earlier publication while this week is pending.</summary>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>The current reading week and its available publication.</returns>
    [HttpGet]
    [ProducesResponseType<WeeklyDvarTorahResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<WeeklyDvarTorahResponse>> Get(CancellationToken cancellationToken)
    {
        var publication = await weeklyDvarTorah.GetCurrentOrLatestAsync(cancellationToken).ConfigureAwait(false);
        return Ok(new WeeklyDvarTorahResponse(
            ToResponse(publication.CurrentWeek),
            publication.Article is null ? null : ToResponse(publication.Article),
            publication.IsCurrentWeek));
    }

    /// <summary>Searches and pages metadata for published past Dvar Torahs.</summary>
    /// <param name="search">Optional title, reading, date, holiday, or tag search.</param>
    /// <param name="page">One-based page number.</param>
    /// <param name="pageSize">Number of records per page.</param>
    /// <param name="readStatus">All, read, or unread teachings for the authenticated account.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>The requested archive metadata page.</returns>
    [HttpGet("archive")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType<WeeklyDvarTorahArchiveResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<WeeklyDvarTorahArchiveResponse>> GetArchive([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = WeeklyDvarTorahService.DefaultArchivePageSize, CancellationToken cancellationToken = default, [FromQuery] string readStatus = "all")
    {
        var validationError = ValidateArchiveQuery(search, page, pageSize) ?? (readStatus is "all" or "read" or "unread" ? null : "Read status must be all, read, or unread.");
        if (validationError is not null)
        {
            return BadRequest(new ProblemDetails { Status = StatusCodes.Status400BadRequest, Title = "Invalid archive query.", Detail = validationError });
        }

        var filter = readStatus == "all" ? null : new WeeklyDvarTorahReadFilter(await readState.GetReadWeekKeysAsync(currentUser.UserId, cancellationToken).ConfigureAwait(false), readStatus == "read");
        var result = await weeklyDvarTorah.SearchArchiveAsync(search, page, pageSize, cancellationToken, filter).ConfigureAwait(false);
        var totalPages = result.TotalCount == 0 ? 0 : ((result.TotalCount - 1) / pageSize) + 1;
        return Ok(new WeeklyDvarTorahArchiveResponse(
            result.Items.Select(ToResponse).ToArray(),
            page,
            pageSize,
            result.TotalCount,
            totalPages));
    }

    /// <summary>Gets one published past Dvar Torah from the archive.</summary>
    /// <param name="weekKey">Stable reading-cycle and Shabbat key.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>The matching published article.</returns>
    [HttpGet("archive/{weekKey}")]
    [ProducesResponseType<WeeklyDvarTorahArticleResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WeeklyDvarTorahArticleResponse>> GetArchived(string weekKey, CancellationToken cancellationToken)
    {
        var article = await weeklyDvarTorah.GetArchivedAsync(weekKey, cancellationToken).ConfigureAwait(false);
        return article is null ? NotFound() : Ok(ToResponse(article));
    }

    /// <summary>Gets the authenticated account's completed teachings.</summary>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>The account's read teaching keys.</returns>
    [HttpGet("read-state")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType<WeeklyDvarTorahReadStateResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<WeeklyDvarTorahReadStateResponse>> GetReadState(CancellationToken cancellationToken)
    {
        return Ok(new WeeklyDvarTorahReadStateResponse(await readState.GetReadWeekKeysAsync(currentUser.UserId, cancellationToken).ConfigureAwait(false)));
    }

    /// <summary>Marks a current or past published teaching as read or unread for this account.</summary>
    /// <param name="weekKey">Stable teaching key.</param>
    /// <param name="request">The desired read state.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>No content when saved, or not found for an unavailable teaching.</returns>
    [HttpPut("read-state/{weekKey}")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetReadState(string weekKey, UpdateDvarTorahReadStateRequest request, CancellationToken cancellationToken)
    {
        if (await weeklyDvarTorah.GetPublishedAsync(weekKey, cancellationToken).ConfigureAwait(false) is null)
        {
            return NotFound();
        }
        await readState.SetReadStateAsync(currentUser.UserId, weekKey, request.IsRead, clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    private WeeklyDvarTorahArticleResponse ToResponse(WeeklyDvarTorahArticle article) => new(
        ToResponse(article.Week),
        article.Title,
        article.Body,
        article.Metadata?.CentralTeaching,
        article.Metadata?.Tags ?? [],
        article.Metadata?.Sources.Select(ToResponse).ToArray() ?? [],
        article.Metadata?.TorahGroundingPercent,
        article.GeneratedAtUtc,
        article.PublishedAtUtc,
        ToAudioResponse(article));

    private WeeklyDvarTorahAudioResponse? ToAudioResponse(WeeklyDvarTorahArticle article)
    {
        if (!audioOptions.Enabled || article.Audio is not { } audio)
        {
            return null;
        }

        var basePath = $"/api/dvar-torah/archive/{Uri.EscapeDataString(article.Week.WeekKey)}/audio";
        var version = Uri.EscapeDataString(audio.Version);
        return new WeeklyDvarTorahAudioResponse(audio.Version, audio.Voice, audio.DurationMs, $"{basePath}?version={version}", $"{basePath}/timings?version={version}");
    }

    private static WeeklyDvarTorahSourceResponse ToResponse(WeeklyDvarTorahSource source) => new(
        source.SourceId,
        source.Kind,
        source.Title,
        source.Publisher,
        source.SourceUrl,
        source.Excerpt,
        source.RetrievedAtUtc,
        source.CanonicalReference,
        source.PublishedAtUtc,
        source.License);

    private static WeeklyDvarTorahArchiveItemResponse ToResponse(WeeklyDvarTorahArchiveItem item) => new(
        ToResponse(item.Week),
        item.Title,
        item.Tags.Take(3).ToArray(),
        item.PublishedAtUtc);

    private static WeeklyDvarTorahWeekResponse ToResponse(WeeklyDvarTorahWeek week) => new(
        week.WeekKey,
        week.ShabbatDate,
        week.HebrewDate,
        week.Parashah,
        week.Holiday,
        week.InIsrael);

    private static string? ValidateArchiveQuery(string? search, int page, int pageSize)
    {
        if (page < 1)
        {
            return "Page must be at least one.";
        }
        if (pageSize is < 1 or > WeeklyDvarTorahService.MaximumArchivePageSize)
        {
            return $"Page size must be between one and {WeeklyDvarTorahService.MaximumArchivePageSize}.";
        }
        if ((long)(page - 1) * pageSize > int.MaxValue)
        {
            return "The requested page is too large.";
        }
        if (search?.Trim().Length > WeeklyDvarTorahService.MaximumArchiveSearchCharacters)
        {
            return $"Search cannot exceed {WeeklyDvarTorahService.MaximumArchiveSearchCharacters} characters.";
        }

        return null;
    }
}
