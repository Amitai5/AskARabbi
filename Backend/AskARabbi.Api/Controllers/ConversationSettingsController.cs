using AskARabbi.Api.Authentication;
using AskARabbi.Api.Calendar;
using AskARabbi.Api.Contracts.ConversationSettings;
using AskARabbiLIB.ConversationSettings;
using AskARabbiLIB.Calendar;
using AskARabbiLIB.Usage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AskARabbi.Api.Controllers;

/// <summary>Provides account usage and conversation personalization settings.</summary>
[ApiController]
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/conversation-settings")]
public sealed class ConversationSettingsController : ControllerBase
{
    private readonly ConversationSettingsService settings;
    private readonly MonthlyUsageService usage;
    private readonly ICurrentUser currentUser;
    private readonly PersonalizationLocationResolver locations;

    /// <summary>Initializes the conversation-settings API.</summary>
    /// <param name="settings">Conversation-settings application service.</param>
    /// <param name="usage">Monthly usage service.</param>
    /// <param name="currentUser">Current authenticated user accessor.</param>
    /// <param name="locations">Server-authoritative location resolver.</param>
    public ConversationSettingsController(ConversationSettingsService settings, MonthlyUsageService usage, ICurrentUser currentUser, PersonalizationLocationResolver locations)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.usage = usage ?? throw new ArgumentNullException(nameof(usage));
        this.currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        this.locations = locations ?? throw new ArgumentNullException(nameof(locations));
    }

    /// <summary>Gets supported cities for personalization and signup location selection.</summary>
    /// <returns>The reviewed location catalog.</returns>
    [HttpGet("locations")]
    public ActionResult<IReadOnlyList<CalendarLocation>> GetLocations() => Ok(CalendarLocationCatalog.Cities);

    /// <summary>Gets usage for the exact current UTC calendar-month billing period.</summary>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>Current usage and exact period dates.</returns>
    [HttpGet("usage")]
    [ProducesResponseType<UsageResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<UsageResponse>> GetUsage(CancellationToken cancellationToken)
    {
        var value = await usage.GetCurrentAsync(currentUser.UserId, cancellationToken).ConfigureAwait(false);
        return Ok(UsageResponse.FromUsage(value));
    }

    /// <summary>Gets the current personalization settings.</summary>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>A configured or unconfigured personalization envelope.</returns>
    [HttpGet("personalization")]
    [ProducesResponseType<PersonalizationEnvelopeResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PersonalizationEnvelopeResponse>> GetPersonalization(CancellationToken cancellationToken)
    {
        var value = await settings.GetPersonalizationAsync(currentUser.UserId, cancellationToken).ConfigureAwait(false);
        return Ok(new PersonalizationEnvelopeResponse(value is not null, value is null ? null : ToResponse(value)));
    }

    /// <summary>Validates and replaces the current personalization settings.</summary>
    /// <param name="request">Updated personalization.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>The saved normalized personalization.</returns>
    [HttpPut("personalization")]
    [ProducesResponseType<PersonalizationEnvelopeResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PersonalizationEnvelopeResponse>> UpdatePersonalization(PersonalizationRequest request, CancellationToken cancellationToken)
    {
        if (request.BirthDateTime.Kind != DateTimeKind.Unspecified)
        {
            throw new ArgumentException("BirthDateTime must be a local date and time without a UTC offset.", nameof(request));
        }

        var previous = await settings.GetPersonalizationAsync(currentUser.UserId, cancellationToken).ConfigureAwait(false);
        var birthTask = request.BirthLocation is null ? Task.FromResult(previous?.BirthLocation) : locations.ResolveAsync(request.BirthLocation, previous?.BirthLocation, cancellationToken);
        var currentTask = request.CurrentLocation is null ? Task.FromResult(previous?.CurrentLocation)
            : request.CurrentLocation == request.BirthLocation ? birthTask : locations.ResolveAsync(request.CurrentLocation, previous?.CurrentLocation, cancellationToken);
        await Task.WhenAll(birthTask, currentTask).ConfigureAwait(false);
        var birthLocation = await birthTask.ConfigureAwait(false);
        var currentLocation = await currentTask.ConfigureAwait(false);
        if ((request.BirthLocation is not null && birthLocation is null) || (request.CurrentLocation is not null && currentLocation is null))
        {
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Location lookup unavailable", detail: "Your personalization was not changed. Please try saving again shortly.");
        }

        var personalization = new PersonalizationSettings
        {
            FullName = request.FullName,
            BirthDate = DateOnly.FromDateTime(request.BirthDateTime),
            BirthTime = TimeOnly.FromDateTime(request.BirthDateTime),
            BirthTimeZone = birthLocation?.TimeZone ?? request.BirthTimeZone,
            BirthLocation = birthLocation,
            CurrentLocation = currentLocation,
            ConversationLanguage = request.ConversationLanguage,
            QuotationLanguage = request.QuotationLanguage,
            ReligiousMovement = request.ReligiousMovement,
            JewishHeritage = request.JewishHeritage,
            AdditionalContext = request.AdditionalContext,
        };
        var saved = await settings.UpdatePersonalizationAsync(currentUser.UserId, personalization, cancellationToken).ConfigureAwait(false);
        return Ok(new PersonalizationEnvelopeResponse(true, ToResponse(saved)));
    }

    /// <summary>Gets account-backed defaults for new conversations.</summary>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>The current conversation preferences.</returns>
    [HttpGet("preferences")]
    [ProducesResponseType<ConversationPreferencesResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ConversationPreferencesResponse>> GetPreferences(CancellationToken cancellationToken)
    {
        var value = await settings.GetPreferencesAsync(currentUser.UserId, cancellationToken).ConfigureAwait(false);
        return Ok(ToResponse(value));
    }

    /// <summary>Replaces account-backed defaults for new conversations.</summary>
    /// <param name="request">Updated conversation preferences.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>The saved conversation preferences.</returns>
    [HttpPut("preferences")]
    [ProducesResponseType<ConversationPreferencesResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ConversationPreferencesResponse>> UpdatePreferences(ConversationPreferencesRequest request, CancellationToken cancellationToken)
    {
        var value = new ConversationPreferences
        {
            ShowSourceContextByDefault = request.ShowSourceContextByDefault,
            EmailProductUpdates = request.EmailProductUpdates,
            EnterSendsMessage = request.EnterSendsMessage ?? (await settings.GetPreferencesAsync(currentUser.UserId, cancellationToken).ConfigureAwait(false)).EnterSendsMessage,
        };
        var saved = await settings.UpdatePreferencesAsync(currentUser.UserId, value, cancellationToken).ConfigureAwait(false);
        return Ok(ToResponse(saved));
    }

    /// <summary>Gets reading preferences for the authenticated account.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Saved preferences or product defaults.</returns>
    [HttpGet("reading")]
    public async Task<ActionResult<ReadingPreferences>> GetReadingPreferences(CancellationToken cancellationToken)
    {
        return Ok(await settings.GetReadingPreferencesAsync(currentUser.UserId, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Saves reading preferences without changing other account settings.</summary>
    /// <param name="request">Supported reading presets.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The saved reading preferences.</returns>
    [HttpPut("reading")]
    public async Task<ActionResult<ReadingPreferences>> UpdateReadingPreferences(ReadingPreferencesRequest request, CancellationToken cancellationToken)
    {
        var value = new ReadingPreferences { TextSize = request.TextSize, LineSpacing = request.LineSpacing, Theme = request.Theme, FocusLongContent = request.FocusLongContent };
        return Ok(await settings.UpdateReadingPreferencesAsync(currentUser.UserId, value, cancellationToken).ConfigureAwait(false));
    }

    private static PersonalizationResponse ToResponse(PersonalizationSettings value) => new(
        value.FullName,
        new DateTime(value.BirthDate, value.BirthTime, DateTimeKind.Unspecified),
        value.BirthTimeZone,
        value.ConversationLanguage,
        value.QuotationLanguage,
        value.ReligiousMovement,
        value.JewishHeritage,
        value.AdditionalContext,
        value.BirthLocation,
        value.CurrentLocation);

    private static ConversationPreferencesResponse ToResponse(ConversationPreferences value) => new(value.ShowSourceContextByDefault, value.EmailProductUpdates, value.EnterSendsMessage);
}
