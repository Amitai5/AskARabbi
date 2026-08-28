namespace AskARabbi.Api.Contracts.ConversationSettings;

/// <summary>Provides the saved personalization context for the authenticated user.</summary>
public sealed record PersonalizationResponse(string FullName, DateTime BirthDateTime, string BirthTimeZone, string ConversationLanguage, string QuotationLanguage, string ReligiousMovement, string JewishHeritage, string? AdditionalContext);
