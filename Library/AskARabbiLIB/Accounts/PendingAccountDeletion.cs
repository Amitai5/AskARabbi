namespace AskARabbiLIB.Accounts;

/// <summary>Minimal identity needed to finish an account erasure after a dependency failure.</summary>
/// <param name="UserId">Application account ID.</param>
/// <param name="ProviderUserId">WorkOS identity to delete before removing the recovery record.</param>
public sealed record PendingAccountDeletion(Guid UserId, string ProviderUserId);
