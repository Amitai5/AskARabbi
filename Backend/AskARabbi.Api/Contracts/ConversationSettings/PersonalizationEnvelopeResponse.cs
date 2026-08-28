namespace AskARabbi.Api.Contracts.ConversationSettings;

/// <summary>Distinguishes an unconfigured profile from a configured profile.</summary>
public sealed record PersonalizationEnvelopeResponse(bool IsConfigured, PersonalizationResponse? Personalization);
