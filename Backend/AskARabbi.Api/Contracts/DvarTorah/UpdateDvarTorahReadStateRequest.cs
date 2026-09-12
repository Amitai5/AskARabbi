namespace AskARabbi.Api.Contracts.DvarTorah;

/// <summary>Sets explicit reading progress for a single published teaching.</summary>
public sealed record UpdateDvarTorahReadStateRequest
{
    /// <summary>Whether the account has completed the teaching.</summary>
    public required bool IsRead { get; init; }
}
