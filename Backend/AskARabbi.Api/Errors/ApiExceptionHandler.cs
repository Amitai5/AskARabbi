using AskARabbi.Api.Authentication;
using AskARabbiLIB.Persistence.Mongo;
using AskARabbiLIB.Usage;
using AskARabbi.Api.Contracts.ConversationSettings;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AskARabbi.Api.Errors;

/// <summary>Converts known API-boundary failures into stable problem responses.</summary>
public sealed class ApiExceptionHandler : IExceptionHandler
{
    private readonly ILogger<ApiExceptionHandler> logger;

    /// <summary>Initializes the API exception handler.</summary>
    /// <param name="logger">Structured logger.</param>
    public ApiExceptionHandler(ILogger<ApiExceptionHandler> logger)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        var (status, title, detail, code) = exception switch
        {
            ChatUsageException { Code: "usage_limit_reached" } limited => (StatusCodes.Status429TooManyRequests, "Monthly chat allowance reached", limited.Message, limited.Code),
            ChatUsageException { Code: "chat_in_progress" } busy => (StatusCodes.Status409Conflict, "Answer in progress", busy.Message, busy.Code),
            ChatUsageException accounting => (StatusCodes.Status503ServiceUnavailable, "Chat usage unavailable", accounting.Message, accounting.Code),
            UnauthenticatedRequestException => (StatusCodes.Status401Unauthorized, "Authentication required", "Sign in before using this endpoint.", "authentication_required"),
            IdentityRequestRejectedException rejected => (StatusCodes.Status400BadRequest, "Authentication request rejected", rejected.Message, "authentication_rejected"),
            IdentityProviderUnavailableException => (StatusCodes.Status503ServiceUnavailable, "Authentication unavailable", "The identity service is unavailable or not configured.", "authentication_unavailable"),
            PersistenceUnavailableException => (StatusCodes.Status503ServiceUnavailable, "Persistence unavailable", "The application database is unavailable or not configured.", "persistence_unavailable"),
            ArgumentException argument => (StatusCodes.Status400BadRequest, "Invalid request", argument.Message, "invalid_request"),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected server error", "The request could not be completed.", "server_error"),
        };

        if (status >= StatusCodes.Status500InternalServerError)
        {
            if (exception is IdentityProviderUnavailableException or PersistenceUnavailableException)
            {
                logger.LogWarning("A configured external boundary is unavailable. ExceptionType: {ExceptionType}", exception.GetType().Name);
            }
            else
            {
                logger.LogError(exception, "Unhandled API request failure. TraceId: {TraceId}", httpContext.TraceIdentifier);
            }
        }

        httpContext.Response.StatusCode = status;
        httpContext.Response.ContentType = "application/problem+json";
        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path,
        };
        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = httpContext.TraceIdentifier;
        if (exception is ChatUsageException { Usage: { } usage })
        {
            problem.Extensions["usage"] = UsageResponse.FromUsage(usage);
            if (usage.IsLimitReached)
            {
                httpContext.Response.Headers.RetryAfter = usage.PeriodEndUtc.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            }
        }
        await httpContext.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json", cancellationToken).ConfigureAwait(false);
        return true;
    }
}
