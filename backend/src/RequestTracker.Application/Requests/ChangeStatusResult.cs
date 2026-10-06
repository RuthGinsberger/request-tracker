using RequestTracker.Domain;

namespace RequestTracker.Application.Requests;

/// <summary>Expected outcomes of a single status update; controllers map them to HTTP.</summary>
public abstract record ChangeStatusResult
{
    private ChangeStatusResult()
    {
    }

    public sealed record Updated(RequestDetailsDto Request) : ChangeStatusResult;

    public sealed record NotFound : ChangeStatusResult;

    public sealed record Conflict(RequestStatus CurrentStatus, string CurrentVersion) : ChangeStatusResult;

    public sealed record InvalidTransition(RequestStatus CurrentStatus, RequestStatus RequestedStatus) : ChangeStatusResult;
}
