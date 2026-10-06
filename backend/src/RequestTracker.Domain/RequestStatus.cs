namespace RequestTracker.Domain;

/// <summary>Request lifecycle status. Explicit values are persisted as tinyint; do not renumber.</summary>
public enum RequestStatus : byte
{
    New = 1,
    InProgress = 2,
    Waiting = 3,
    Completed = 4,
}
