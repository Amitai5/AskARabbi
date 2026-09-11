using AskARabbiLIB.Calendar;

namespace AskARabbiLIB.ConversationSettings;

/// <summary>Coordinates personalization retrieval, normalization, and validation.</summary>
public sealed class ConversationSettingsService
{
    private readonly IConversationSettingsStore store;
    private readonly TimeProvider timeProvider;
    private readonly ICalendarPreferencesStore? legacyCalendarStore;

    /// <summary>Initializes a conversation settings service.</summary>
    /// <param name="store">Conversation-settings persistence boundary.</param>
    /// <param name="timeProvider">Optional source of UTC time.</param>
    /// <param name="legacyCalendarStore">Optional source for reading older accounts' saved current locations.</param>
    public ConversationSettingsService(IConversationSettingsStore store, TimeProvider? timeProvider = null, ICalendarPreferencesStore? legacyCalendarStore = null)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.legacyCalendarStore = legacyCalendarStore;
    }

    /// <summary>Gets a user's current personalization.</summary>
    /// <param name="userId">Owning user ID.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>Personalization when configured; otherwise, <see langword="null"/>.</returns>
    public async Task<PersonalizationSettings?> GetPersonalizationAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);
        var value = await store.GetPersonalizationAsync(userId, cancellationToken).ConfigureAwait(false);
        if (value is not null && value.CurrentLocation is null && legacyCalendarStore is not null)
        {
            var legacy = await legacyCalendarStore.GetCalendarPreferencesAsync(userId, cancellationToken).ConfigureAwait(false);
            value = value with { CurrentLocation = legacy?.Location };
        }
        return value;
    }

    /// <summary>Normalizes, validates, and saves a user's personalization.</summary>
    /// <param name="userId">Owning user ID.</param>
    /// <param name="personalization">Personalization to save.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>The saved normalized personalization.</returns>
    public async Task<PersonalizationSettings> UpdatePersonalizationAsync(Guid userId, PersonalizationSettings personalization, CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);
        ArgumentNullException.ThrowIfNull(personalization);
        var now = timeProvider.GetUtcNow();
        var normalized = personalization.NormalizeAndValidate(DateOnly.FromDateTime(now.UtcDateTime));
        await store.UpsertPersonalizationAsync(userId, normalized, now, cancellationToken).ConfigureAwait(false);
        return normalized;
    }

    /// <summary>Gets a user's account-backed conversation preferences.</summary>
    /// <param name="userId">Owning user ID.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>Stored preferences or the product defaults when none have been saved.</returns>
    public async Task<ConversationPreferences> GetPreferencesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);
        return await store.GetPreferencesAsync(userId, cancellationToken).ConfigureAwait(false) ?? new ConversationPreferences();
    }

    /// <summary>Saves a user's account-backed conversation preferences.</summary>
    /// <param name="userId">Owning user ID.</param>
    /// <param name="preferences">Preferences to save.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>The saved preferences.</returns>
    public async Task<ConversationPreferences> UpdatePreferencesAsync(Guid userId, ConversationPreferences preferences, CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);
        ArgumentNullException.ThrowIfNull(preferences);
        await store.UpsertPreferencesAsync(userId, preferences, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        return preferences;
    }

    /// <summary>Gets saved reading preferences or backward-compatible defaults.</summary>
    /// <param name="userId">Owning user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The user's reading preferences.</returns>
    public async Task<ReadingPreferences> GetReadingPreferencesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);
        return await store.GetReadingPreferencesAsync(userId, cancellationToken).ConfigureAwait(false) ?? new ReadingPreferences();
    }

    /// <summary>Validates and saves reading preferences independently of other settings.</summary>
    /// <param name="userId">Owning user ID.</param>
    /// <param name="preferences">New reading preferences.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The saved preferences.</returns>
    public async Task<ReadingPreferences> UpdateReadingPreferencesAsync(Guid userId, ReadingPreferences preferences, CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);
        ArgumentNullException.ThrowIfNull(preferences);
        preferences.Validate();
        await store.UpsertReadingPreferencesAsync(userId, preferences, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        return preferences;
    }

    private static void ValidateUserId(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User ID is required.", nameof(userId));
        }
    }
}
