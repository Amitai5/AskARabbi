namespace AskARabbiLIB.ConversationSettings;

/// <summary>Persists user-owned conversation personalization settings.</summary>
public interface IConversationSettingsStore
{
    /// <summary>Gets a user's reading preferences.</summary>
    /// <param name="userId">Owning user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Saved reading preferences, or null for an unconfigured account.</returns>
    Task<ReadingPreferences?> GetReadingPreferencesAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Updates only reading preferences, preserving other settings.</summary>
    /// <param name="userId">Owning user ID.</param>
    /// <param name="preferences">Validated reading preferences.</param>
    /// <param name="updatedAtUtc">UTC update time.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The persistence operation.</returns>
    Task UpsertReadingPreferencesAsync(Guid userId, ReadingPreferences preferences, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default);

    /// <summary>Gets personalization for a user.</summary>
    /// <param name="userId">Owning user ID.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>Personalization when configured; otherwise, <see langword="null"/>.</returns>
    Task<PersonalizationSettings?> GetPersonalizationAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Creates or replaces personalization for a user.</summary>
    /// <param name="userId">Owning user ID.</param>
    /// <param name="personalization">Validated personalization.</param>
    /// <param name="updatedAtUtc">UTC update time.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>A task representing the operation.</returns>
    Task UpsertPersonalizationAsync(Guid userId, PersonalizationSettings personalization, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default);

    /// <summary>Gets conversation preferences for a user.</summary>
    /// <param name="userId">Owning user ID.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>Stored preferences when configured; otherwise, <see langword="null"/>.</returns>
    Task<ConversationPreferences?> GetPreferencesAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Creates or replaces conversation preferences for a user.</summary>
    /// <param name="userId">Owning user ID.</param>
    /// <param name="preferences">Preferences to persist.</param>
    /// <param name="updatedAtUtc">UTC update time.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>A task representing the operation.</returns>
    Task UpsertPreferencesAsync(Guid userId, ConversationPreferences preferences, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default);
}
