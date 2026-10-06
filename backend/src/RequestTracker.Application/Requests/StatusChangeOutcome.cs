using RequestTracker.Domain;

namespace RequestTracker.Application.Requests;

/// <summary>Outcome of applying one status change to a loaded request; shared by the single and bulk updates.</summary>
public abstract record StatusChangeOutcome
{
    private StatusChangeOutcome()
    {
    }

    /// <summary>Committed (status, UpdatedAt and the audit row in one transaction); <paramref name="Request"/> holds the new version.</summary>
    public sealed record Updated(Request Request) : StatusChangeOutcome;

    /// <summary>The client's version is stale (pre-check or database predicate); nothing was written.</summary>
    public sealed record Conflict(RequestStatus CurrentStatus, string CurrentVersion) : StatusChangeOutcome;

    public sealed record InvalidTransition(RequestStatus CurrentStatus, RequestStatus RequestedStatus) : StatusChangeOutcome;

    /// <summary>The row disappeared between the read and the write.</summary>
    public sealed record NotFound : StatusChangeOutcome;
}
