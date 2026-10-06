namespace RequestTracker.Application.Requests;

/// <summary>Expected outcomes of loading one request; controllers map them to HTTP.</summary>
public abstract record GetRequestResult
{
    private GetRequestResult()
    {
    }

    public sealed record Found(RequestDetailsDto Request) : GetRequestResult;

    public sealed record NotFound : GetRequestResult;
}
