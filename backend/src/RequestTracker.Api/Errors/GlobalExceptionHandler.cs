using Microsoft.AspNetCore.Diagnostics;

namespace RequestTracker.Api.Errors;

/// <summary>
/// Turns unexpected exceptions into <c>500 internal-error</c> ProblemDetails and logs each one once.
/// A cancellation caused by the client aborting the request is expected: no error log, no response body.
/// </summary>
public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    IHostEnvironment environment,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    /// <summary>Client closed the connection (nginx convention; nothing is sent back anyway).</summary>
    public const int ClientClosedRequestStatusCode = 499;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (IsClientAbort(httpContext, exception))
        {
            logger.LogDebug("Request was cancelled by the client: {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
            httpContext.Response.StatusCode = ClientClosedRequestStatusCode;
            return true;
        }

        logger.LogError(exception, "Unhandled exception while processing {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);

        var isDevelopment = environment.IsDevelopment();
        var problem = ApiProblems.Internal(
            httpContext,
            isDevelopment ? exception.Message : "The server could not process the request.");
        if (isDevelopment)
        {
            problem.Extensions["stackTrace"] = exception.ToString();
        }

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    public static bool IsClientAbort(HttpContext httpContext, Exception exception) =>
        exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested;
}
