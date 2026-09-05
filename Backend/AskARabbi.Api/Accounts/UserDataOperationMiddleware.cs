using AskARabbi.Api.Authentication;
using AskARabbiLIB.Accounts;

namespace AskARabbi.Api.Accounts;

/// <summary>Prevents erasure racing with writes on any API replica. Ordinary reads do not take a write lease.</summary>
/// <param name="next">Next request handler.</param>
/// <param name="logger">Structured operation logger.</param>
public sealed class UserDataOperationMiddleware(RequestDelegate next, ILogger<UserDataOperationMiddleware> logger)
{
    /// <summary>Runs authenticated mutations under a bounded shared lease.</summary>
    /// <param name="context">Current HTTP request.</param>
    /// <param name="data">Distributed operation boundary.</param>
    /// <param name="currentUser">Authenticated owner.</param>
    /// <param name="timeProvider">UTC clock.</param>
    /// <returns>The middleware task.</returns>
    public async Task InvokeAsync(HttpContext context, IUserDataStore data, ICurrentUser currentUser, TimeProvider timeProvider)
    {
        if (context.User.Identity?.IsAuthenticated != true || HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method) || HttpMethods.IsOptions(context.Request.Method) || context.GetEndpoint()?.Metadata.GetMetadata<ManagesUserDataOperationAttribute>() is not null)
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var userId = currentUser.UserId;
        var operationId = Guid.NewGuid();
        var now = timeProvider.GetUtcNow();
        if (!await data.TryAcquireAsync(userId, operationId, false, now, now.AddMinutes(30), context.RequestAborted).ConfigureAwait(false))
        {
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            await context.Response.WriteAsJsonAsync(new { detail = "Your account data is being deleted. Please wait before making more changes." }, context.RequestAborted).ConfigureAwait(false);
            return;
        }

        var originalCancellation = context.RequestAborted;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(originalCancellation);
        timeout.CancelAfter(TimeSpan.FromMinutes(5));
        context.RequestAborted = timeout.Token;
        try
        {
            await next(context).ConfigureAwait(false);
        }
        finally
        {
            context.RequestAborted = originalCancellation;
            using var releaseTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try
            {
                await data.ReleaseAsync(userId, operationId, releaseTimeout.Token).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Could not release data-operation lease for {UserId}; its crash-recovery expiry remains in effect.", userId);
            }
        }
    }
}
