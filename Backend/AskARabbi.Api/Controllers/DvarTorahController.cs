using AskARabbi.Api.Contracts.DvarTorah;
using AskARabbiLIB.DvarTorah;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AskARabbi.Api.Controllers;

/// <summary>Loads the current or most recent published weekly Dvar Torah.</summary>
[ApiController]
[Authorize]
[Route("api/dvar-torah")]
public sealed class DvarTorahController : ControllerBase
{
    private readonly WeeklyDvarTorahService weeklyDvarTorah;

    /// <summary>Initializes the weekly Dvar Torah API.</summary>
    /// <param name="weeklyDvarTorah">Weekly publication service.</param>
    public DvarTorahController(WeeklyDvarTorahService weeklyDvarTorah)
    {
        this.weeklyDvarTorah = weeklyDvarTorah ?? throw new ArgumentNullException(nameof(weeklyDvarTorah));
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

    private static WeeklyDvarTorahArticleResponse ToResponse(WeeklyDvarTorahArticle article) => new(
        ToResponse(article.Week),
        article.Title,
        article.Body,
        article.GeneratedAtUtc,
        article.PublishedAtUtc);

    private static WeeklyDvarTorahWeekResponse ToResponse(WeeklyDvarTorahWeek week) => new(
        week.WeekKey,
        week.ShabbatDate,
        week.HebrewDate,
        week.Parashah,
        week.Holiday,
        week.InIsrael);
}
