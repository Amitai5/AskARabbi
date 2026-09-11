using AskARabbi.Api.Authentication;
using AskARabbi.Api.Calendar;
using AskARabbiLIB.Calendar;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AskARabbi.Api.Controllers;

/// <summary>Provides account-scoped calendar access independently of chat allowance.</summary>
[ApiController]
[Authorize]
[Route("api/calendar")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CalendarController(CalendarOverviewService overview, CalendarPreferencesService preferences, ICurrentUser currentUser) : ControllerBase
{
    public sealed record PreferencesResponse(CalendarPreferences Preferences, IReadOnlyList<CalendarLocation> Cities);

    /// <summary>Gets deterministic dates and applicable external holiday/local timing data.</summary>
    /// <param name="days">30, 90, or 365 days; defaults to 90.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An explicitly complete or partial overview.</returns>
    [HttpGet("overview")]
    public async Task<ActionResult<CalendarOverview>> GetOverviewAsync(CancellationToken cancellationToken, [FromQuery] int days = 90) => Ok(await overview.GetAsync(currentUser.UserId, days, cancellationToken).ConfigureAwait(false));

    /// <summary>Gets saved preferences and the reviewed searchable city list.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Saved preferences or explicit defaults.</returns>
    [HttpGet("preferences")]
    public async Task<ActionResult<PreferencesResponse>> GetPreferencesAsync(CancellationToken cancellationToken) => Ok(new PreferencesResponse(await preferences.GetAsync(currentUser.UserId, cancellationToken).ConfigureAwait(false), CalendarLocationCatalog.Cities));

    /// <summary>Validates and saves only the authenticated owner's calendar choices.</summary>
    /// <param name="request">Calendar choices; location metadata is resolved server-side.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Normalized saved preferences, or a transient ZIP resolution error.</returns>
    [HttpPut("preferences")]
    public async Task<ActionResult<CalendarPreferences>> UpdatePreferencesAsync(CalendarPreferences request, CancellationToken cancellationToken)
    {
        var saved = await preferences.UpdateAsync(currentUser.UserId, request, cancellationToken).ConfigureAwait(false);
        return saved is null ? Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Location lookup unavailable", detail: "Your settings were not changed. Try again shortly or choose a supported city.") : Ok(saved);
    }
}
