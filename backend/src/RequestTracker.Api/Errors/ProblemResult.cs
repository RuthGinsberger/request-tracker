using Microsoft.AspNetCore.Mvc;

namespace RequestTracker.Api.Errors;

/// <summary>An action result that writes a ProblemDetails with the right status and media type.</summary>
public sealed class ProblemResult : ObjectResult
{
    public ProblemResult(ProblemDetails problem)
        : base(problem)
    {
        StatusCode = problem.Status;
        ContentTypes.Add(ApiProblems.ContentType);
    }
}
