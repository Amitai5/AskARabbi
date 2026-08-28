using AskARabbi.Api.Authentication;
using AskARabbi.Api.Contracts.ConversationSettings;
using AskARabbiLIB.ConversationSettings;
using AskARabbiLIB.Usage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AskARabbi.Api.Controllers;

/// <summary>Provides account usage and conversation personalization settings.</summary>
[ApiController]
[Authorize]
[Route("api/conversation-settings")]
public sealed class ConversationSettingsController : ControllerBase
{
    private readonly ConversationSettingsService settings;
    private readonly MonthlyUsageService usage;
    private readonly ICurrentUser currentUser;

    /// <summary>Initializes the conversation-settings API.</summary>
    /// <param name="settings">Conversation-settings application service.</param>
    /// <param name="usage">Monthly usage service.</param>
    /// <param name="currentUser">Current authenticated user accessor.</param>
    public ConversationSettingsController(ConversationSettingsService settings, MonthlyUsageService usage, ICurrentUser currentUser)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.usage = usage ?? throw new ArgumentNullException(nameof(usage));
        this.currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    /// <summary>Gets usage for the exact current UTC calendar-month billing period.</summary>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>Current usage and exact period dates.</returns>
    [HttpGet("usage")]
    [ProducesResponseType<UsageResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<UsageResponse>> GetUsage(CancellationToken cancellationToken)
    {
        var value = await usage.GetCurrentAsync(currentUser.UserId, cancellationToken).ConfigureAwait(false);
        return Ok(new UsageResponse(value.PeriodStartUtc, value.PeriodEndUtc, value.AnswersUsed, value.AnswerLimit, value.AnswersRemaining));
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

        var personalization = new PersonalizationSettings
        {
            FullName = request.FullName,
            BirthDate = DateOnly.FromDateTime(request.BirthDateTime),
            BirthTime = TimeOnly.FromDateTime(request.BirthDateTime),
            BirthTimeZone = request.BirthTimeZone,
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
        };
        var saved = await settings.UpdatePreferencesAsync(currentUser.UserId, value, cancellationToken).ConfigureAwait(false);
        return Ok(ToResponse(saved));
    }

    private static PersonalizationResponse ToResponse(PersonalizationSettings value) => new(
        value.FullName,
        new DateTime(value.BirthDate, value.BirthTime, DateTimeKind.Unspecified),
        value.BirthTimeZone,
        value.ConversationLanguage,
        value.QuotationLanguage,
        value.ReligiousMovement,
        value.JewishHeritage,
        value.AdditionalContext);

    private static ConversationPreferencesResponse ToResponse(ConversationPreferences value) => new(value.ShowSourceContextByDefault, value.EmailProductUpdates);
}
