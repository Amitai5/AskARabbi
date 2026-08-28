using System.Security.Claims;

namespace AskARabbi.Api.Authentication;

/// <summary>Reads the authenticated AskRabbi user ID from the current HTTP principal.</summary>
public sealed class HttpCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor contextAccessor;

    /// <summary>Initializes a current-user accessor.</summary>
    /// <param name="contextAccessor">HTTP context accessor.</param>
    public HttpCurrentUser(IHttpContextAccessor contextAccessor)
    {
        this.contextAccessor = contextAccessor ?? throw new ArgumentNullException(nameof(contextAccessor));
    }

    /// <inheritdoc/>
    public Guid UserId
    {
        get
        {
            var value = contextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var userId) && userId != Guid.Empty ? userId : throw new UnauthenticatedRequestException();
        }
    }
}
