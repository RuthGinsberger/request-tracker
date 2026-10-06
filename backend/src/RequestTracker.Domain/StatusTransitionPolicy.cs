namespace RequestTracker.Domain;

/// <summary>
/// The single source of truth for allowed status transitions (A7). Pure and table-driven: no I/O.
/// Same-status is never allowed and Completed is terminal.
/// </summary>
public static class StatusTransitionPolicy
{
    private static readonly IReadOnlyDictionary<RequestStatus, IReadOnlyList<RequestStatus>> Transitions =
        new Dictionary<RequestStatus, IReadOnlyList<RequestStatus>>
        {
            [RequestStatus.New] = [RequestStatus.InProgress],
            [RequestStatus.InProgress] = [RequestStatus.Waiting, RequestStatus.Completed],
            [RequestStatus.Waiting] = [RequestStatus.InProgress, RequestStatus.Completed],
            [RequestStatus.Completed] = [],
        };

    public static bool IsAllowed(RequestStatus from, RequestStatus to) =>
        Transitions.TryGetValue(from, out var targets) && targets.Contains(to);

    public static IReadOnlyList<RequestStatus> AllowedTargets(RequestStatus from) =>
        Transitions.TryGetValue(from, out var targets) ? targets : [];
}
