namespace AskARabbiLIB.ConversationSettings;

/// <summary>Coordinates personalization retrieval, normalization, and validation.</summary>
public sealed class ConversationSettingsService
{
    private readonly IConversationSettingsStore store;
    private readonly TimeProvider timeProvider;

    /// <summary>Initializes a conversation settings service.</summary>
    /// <param name="store">Conversation-settings persistence boundary.</param>
    /// <param name="timeProvider">Optional source of UTC time.</param>
    public ConversationSettingsService(IConversationSettingsStore store, TimeProvider? timeProvider = null)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Gets a user's current personalization.</summary>
    /// <param name="userId">Owning user ID.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>Personalization when configured; otherwise, <see langword="null"/>.</returns>
    public Task<PersonalizationSettings?> GetPersonalizationAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);
        return store.GetPersonalizationAsync(userId, cancellationToken);
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

    private static void ValidateUserId(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User ID is required.", nameof(userId));
        }
    }
}
