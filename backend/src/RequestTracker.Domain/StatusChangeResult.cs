namespace RequestTracker.Domain;

/// <summary>Outcome of <see cref="Request.ChangeStatus"/>. A rejected transition is an expected outcome, not an exception.</summary>
public abstract record StatusChangeResult
{
    private StatusChangeResult()
    {
    }

    /// <summary>The change was applied; <paramref name="History"/> must be persisted together with the request.</summary>
    public sealed record Changed(RequestStatusHistory History) : StatusChangeResult;

    /// <summary>The transition is not allowed (including same-status and from Completed). Nothing was modified.</summary>
    public sealed record NotAllowed(RequestStatus From, RequestStatus To) : StatusChangeResult;
}
