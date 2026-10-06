using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using RequestTracker.Domain;

namespace RequestTracker.Api.Errors;

/// <summary>
/// The only place ProblemDetails are built. Every error response carries <c>type</c>, <c>title</c>, <c>status</c>,
/// <c>traceId</c> and <c>errorCode</c>, whether it comes from a controller, model binding, validation, the
/// exception handler or the framework itself (via <see cref="Normalize"/>).
/// </summary>
public static class ApiProblems
{
    public const string ContentType = "application/problem+json";

    public static ProblemDetails NotFound(HttpContext http, string detail) =>
        Normalize(http, new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "Resource not found.",
            Detail = detail,
        }, ErrorCodes.NotFound);

    public static ProblemDetails PreconditionRequired(HttpContext http) =>
        Normalize(http, new ProblemDetails
        {
            Status = StatusCodes.Status428PreconditionRequired,
            Title = "A precondition is required.",
            Detail = "The If-Match header with the request's current version (ETag) is required.",
        }, ErrorCodes.PreconditionRequired);

    public static ProblemDetails InvalidVersion(HttpContext http) =>
        Normalize(http, new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "The If-Match header is invalid.",
            Detail = "If-Match must be one strong ETag, exactly as returned by the API (a quoted value). '*' and weak ETags are not accepted.",
        }, ErrorCodes.InvalidVersion);

    public static ProblemDetails VersionConflict(
        HttpContext http, int id, RequestStatus currentStatus, string currentVersion)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "The request was modified by someone else.",
            Detail = $"Request {id} no longer has the version you sent. Reload it and try again.",
        };
        problem.Extensions["currentStatus"] = currentStatus.ToString();
        problem.Extensions["currentVersion"] = currentVersion;
        return Normalize(http, problem, ErrorCodes.VersionConflict);
    }

    public static ProblemDetails InvalidStatusTransition(
        HttpContext http, RequestStatus currentStatus, RequestStatus requestedStatus)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "The status change is not allowed.",
            Detail = $"A request cannot move from {currentStatus} to {requestedStatus}.",
        };
        problem.Extensions["currentStatus"] = currentStatus.ToString();
        problem.Extensions["requestedStatus"] = requestedStatus.ToString();
        return Normalize(http, problem, ErrorCodes.InvalidStatusTransition);
    }

    public static ProblemDetails Internal(HttpContext http, string detail) =>
        Normalize(http, new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected error occurred.",
            Detail = detail,
        }, ErrorCodes.InternalError);

    /// <summary>Single shape for model-binding and FluentValidation failures.</summary>
    public static ValidationProblemDetails Validation(HttpContext http, IDictionary<string, string[]> errors)
    {
        var problem = new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "One or more validation errors occurred.",
            Detail = "See the errors property for details.",
        };
        return (ValidationProblemDetails)Normalize(http, problem, ErrorCodes.ValidationFailed);
    }

    public static ValidationProblemDetails Validation(HttpContext http, ModelStateDictionary modelState) =>
        Validation(http, modelState
            .Where(entry => entry.Value is { Errors.Count: > 0 })
            .GroupBy(entry => CamelCaseKey(entry.Key))
            .ToDictionary(
                group => group.Key,
                group => group.SelectMany(entry => entry.Value!.Errors).Select(ErrorMessage).ToArray()));

    /// <summary>Fills type, traceId and errorCode; used for our own problems and for framework-generated ones.</summary>
    public static ProblemDetails Normalize(HttpContext http, ProblemDetails problem, string? errorCode = null)
    {
        errorCode ??= problem.Extensions.TryGetValue("errorCode", out var existing) && existing is string code
            ? code
            : DefaultErrorCode(problem.Status);
        problem.Type = ErrorCodes.TypeUri(errorCode);
        problem.Extensions["errorCode"] = errorCode;
        problem.Extensions["traceId"] = Activity.Current?.Id ?? http.TraceIdentifier;
        return problem;
    }

    /// <summary>Property paths are exposed as camelCase to match the JSON contract ("PageSize" becomes "pageSize").</summary>
    public static string CamelCaseKey(string key) =>
        string.Join('.', key.Split('.').Select(segment => System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(segment)));

    private static string DefaultErrorCode(int? status) => status switch
    {
        StatusCodes.Status400BadRequest => ErrorCodes.ValidationFailed,
        StatusCodes.Status404NotFound => ErrorCodes.NotFound,
        StatusCodes.Status500InternalServerError => ErrorCodes.InternalError,
        _ => $"http-{status}",
    };

    private static string ErrorMessage(ModelError error) =>
        string.IsNullOrEmpty(error.ErrorMessage) ? "The value is invalid." : error.ErrorMessage;
}
