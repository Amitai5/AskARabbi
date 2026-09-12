namespace AskARabbiLIB.ConversationSettings;

/// <summary>Stores account-backed defaults that shape new conversations.</summary>
public sealed record ConversationPreferences
{
    /// <summary>Gets whether supporting source context should be shown by default.</summary>
    public bool ShowSourceContextByDefault { get; init; }

    /// <summary>Gets whether the user has opted in to product-update emails.</summary>
    public bool EmailProductUpdates { get; init; }

    /// <summary>Gets whether Enter sends a message instead of adding a new line; modified Enter can always send.</summary>
    public bool EnterSendsMessage { get; init; }
}
