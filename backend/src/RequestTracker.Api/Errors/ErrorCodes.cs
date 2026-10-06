namespace RequestTracker.Api.Errors;

/// <summary><c>errorCode</c> values of the API contract (docs/ARCHITECTURE.md section 5).</summary>
public static class ErrorCodes
{
    public const string ValidationFailed = "validation-failed";
    public const string NotFound = "not-found";
    public const string InvalidVersion = "invalid-version";
    public const string InvalidStatusTransition = "invalid-status-transition";
    public const string VersionConflict = "version-conflict";
    public const string PreconditionRequired = "precondition-required";
    public const string InternalError = "internal-error";

    public static string TypeUri(string errorCode) => $"urn:requesttracker:error:{errorCode}";
}
