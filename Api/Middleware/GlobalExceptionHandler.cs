using Api.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Api.Middleware;

internal sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problem = exception switch
        {
            CacheLockNotAcquiredException lockEx => HandleLockNotAcquired(httpContext, lockEx),
            _ => HandleUnexpected(httpContext, exception)
        };

        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);

        return true;
    }

    private ProblemDetails HandleLockNotAcquired(HttpContext httpContext, CacheLockNotAcquiredException exception)
    {
        logger.LogWarning(exception, "Cache lock not acquired. Returning 503 to caller.");

        httpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;

        httpContext.Response.Headers.RetryAfter = exception.RetryAfterSeconds.ToString();

        return new ProblemDetails
        {
            Title = "Service temporarily unavailable",
            Detail = "The server is busy processing a concurrent request. Please retry after the indicated delay.",
            Status = StatusCodes.Status503ServiceUnavailable,
        };
    }

    private ProblemDetails HandleUnexpected(HttpContext httpContext, Exception exception)
    {
        logger.LogError(exception, "Unhandled exception.");

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        return new ProblemDetails
        {
            Title = "An unexpected error occurred",
            Detail = "An internal server error occurred. Please try again later.",
            Status = StatusCodes.Status500InternalServerError,
        };
    }
}
