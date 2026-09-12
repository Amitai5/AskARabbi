namespace AskARabbi.Api.Contracts.ConversationSettings;

/// <summary>Updates account-backed defaults for new conversations.</summary>
public sealed record ConversationPreferencesRequest
{
    /// <summary>Gets whether supporting source context should be shown by default.</summary>
    public bool ShowSourceContextByDefault { get; init; }

    /// <summary>Gets whether the account has opted in to product-update emails.</summary>
    public bool EmailProductUpdates { get; init; }

    /// <summary>Gets whether Enter sends; omission preserves the saved behavior for older clients.</summary>
    public bool? EnterSendsMessage { get; init; }
}
