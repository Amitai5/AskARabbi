namespace AskARabbi.Api.Contracts.ConversationSettings;

/// <summary>Returns account-backed defaults for new conversations.</summary>
/// <param name="ShowSourceContextByDefault">Whether supporting source context is shown by default.</param>
/// <param name="EmailProductUpdates">Whether the account receives product-update emails.</param>
/// <param name="EnterSendsMessage">Whether Enter sends instead of adding a new line.</param>
public sealed record ConversationPreferencesResponse(bool ShowSourceContextByDefault, bool EmailProductUpdates, bool EnterSendsMessage = false);
