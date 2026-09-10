namespace AskARabbiLIB.Usage;

/// <summary>Identifies exclusive chat accounting ownership for a user and UTC month.</summary>
public sealed record ChatUsageLease(Guid UserId, Guid Id, DateTimeOffset PeriodStartUtc, DateTimeOffset PeriodEndUtc, DateTimeOffset ExpiresAtUtc, long TokenLimit);
