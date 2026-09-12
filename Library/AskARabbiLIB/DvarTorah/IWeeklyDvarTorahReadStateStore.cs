namespace AskARabbiLIB.DvarTorah;

/// <summary>Stores account-owned progress separately from published teachings.</summary>
public interface IWeeklyDvarTorahReadStateStore
{
    /// <summary>Gets the keys of teachings explicitly marked as read by an account.</summary>
    /// <param name="userId">Owning account.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>The read teaching keys, or an empty collection for a new account.</returns>
    Task<IReadOnlyList<string>> GetReadWeekKeysAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Atomically marks one teaching as read or unread without replacing other progress.</summary>
    /// <param name="userId">Owning account.</param>
    /// <param name="weekKey">Validated published teaching key.</param>
    /// <param name="isRead">Whether the teaching has been read.</param>
    /// <param name="updatedAtUtc">Time of the change.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>A task representing persistence of the change.</returns>
    Task SetReadStateAsync(Guid userId, string weekKey, bool isRead, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default);
}
