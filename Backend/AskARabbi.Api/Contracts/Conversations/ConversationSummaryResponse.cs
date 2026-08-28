namespace AskARabbi.Api.Contracts.Conversations;

/// <summary>Provides lightweight navigation data for one conversation.</summary>
public sealed record ConversationSummaryResponse(Guid Id, string Title, IReadOnlyList<string> EnabledSourceKeys, DateTimeOffset UpdatedAtUtc);
