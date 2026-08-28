namespace AskARabbi.Api.Contracts.ConversationSettings;

/// <summary>Provides exact UTC billing dates and included answer usage.</summary>
public sealed record UsageResponse(DateTimeOffset PeriodStartUtc, DateTimeOffset PeriodEndUtc, int AnswersUsed, int AnswerLimit, int AnswersRemaining);
