using AskARabbi.Api.Accounts;
using AskARabbi.Api.Authentication;
using AskARabbiLIB.Accounts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AskARabbi.Api.Controllers;

/// <summary>Provides explicitly confirmed, owner-scoped data erasure.</summary>
/// <param name="data">Distributed data-operation and erasure boundary.</param>
/// <param name="currentUser">Authenticated owner.</param>
/// <param name="deletionService">Durable account cleanup service.</param>
/// <param name="timeProvider">UTC clock.</param>
[ApiController]
[Authorize]
[Route("api/user/data")]
[ManagesUserDataOperation]
public sealed class UserDataController(IUserDataStore data, ICurrentUser currentUser, AccountDeletionService deletionService, TimeProvider timeProvider) : ControllerBase
{
    /// <summary>Deletes all chats and messages, without resetting allowance or preferences.</summary>
    /// <param name="confirmation">Explicit confirmation header; also requires a CORS preflight from browsers.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>204 on completion, or 409 when another write is in progress.</returns>
    [HttpDelete("chats")]
    public async Task<IActionResult> DeleteChats([FromHeader(Name = "X-Confirm-Deletion")] string? confirmation, CancellationToken cancellationToken)
    {
        if (!string.Equals(confirmation, "DELETE ALL CHATS", StringComparison.Ordinal))
        {
            return Problem(statusCode: 400, detail: "Confirm deletion of all chats before continuing.");
        }
        var userId = currentUser.UserId;
        var operationId = Guid.NewGuid();
        var now = timeProvider.GetUtcNow();
        if (!await data.TryAcquireAsync(userId, operationId, true, now, now.AddMinutes(30), cancellationToken).ConfigureAwait(false))
        {
            return Busy();
        }
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(5));
            await data.DeleteChatsAsync(userId, timeout.Token).ConfigureAwait(false);
            return NoContent();
        }
        finally
        {
            using var releaseTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await data.ReleaseAsync(userId, operationId, releaseTimeout.Token).ConfigureAwait(false);
        }
    }

    /// <summary>Disables the account, erases its WorkOS identity and data, and clears the local session.</summary>
    /// <param name="confirmation">Explicit irreversible-account-erasure confirmation.</param>
    /// <param name="cancellationToken">Request cancellation before acceptance.</param>
    /// <returns>Completion status, or accepted pending cleanup if a dependency needs retrying.</returns>
    [HttpDelete("account")]
    public async Task<IActionResult> DeleteAccount([FromHeader(Name = "X-Confirm-Deletion")] string? confirmation, CancellationToken cancellationToken)
    {
        if (!string.Equals(confirmation, "DELETE ACCOUNT", StringComparison.Ordinal))
        {
            return Problem(statusCode: 400, detail: "Confirm deletion of your account before continuing.");
        }
        var deletion = await data.TryRequestDeletionAsync(currentUser.UserId, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        if (deletion is null)
        {
            return Busy();
        }

        // Acceptance is durable. A browser disconnect cannot undo it or strand a half-deleted account.
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme).ConfigureAwait(false);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            if (await deletionService.TryCompleteAsync(deletion, timeout.Token).ConfigureAwait(false))
            {
                return Ok(new { status = "deleted" });
            }
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            // The persisted request is picked up by the recovery worker.
        }
        return Accepted(new { status = "pending" });
    }

    private ObjectResult Busy() => Problem(statusCode: 409, detail: "An answer or account update is still in progress. Wait for it to finish, then try deleting again.");
}
