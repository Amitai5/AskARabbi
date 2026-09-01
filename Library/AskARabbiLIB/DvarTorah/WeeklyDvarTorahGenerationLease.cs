namespace AskARabbiLIB.DvarTorah;

/// <summary>Represents temporary ownership of one idempotent weekly generation attempt.</summary>
/// <param name="Week">Reading week being generated.</param>
/// <param name="LeaseId">Unique invocation identifier that owns the lease.</param>
/// <param name="ExpiresAtUtc">UTC time after which another invocation may recover the work.</param>
public sealed record WeeklyDvarTorahGenerationLease(WeeklyDvarTorahWeek Week, string LeaseId, DateTimeOffset ExpiresAtUtc);
