using RequestTracker.Domain;

namespace RequestTracker.Application.Requests;

/// <summary>Classification of a requested status change against the current row (pure, no I/O).</summary>
public enum StatusChangeCheck
{
    /// <summary>The client's version is current and the transition is allowed.</summary>
    Allowed,

    /// <summary>The client's version differs from the current one; reported before the transition rule (D-049).</summary>
    VersionMismatch,

    /// <summary>The version is current but the transition is not allowed (same status, back to New, from Completed).</summary>
    TransitionNotAllowed,
}

public static class StatusChangeClassifier
{
    /// <summary>Version first (409), then the transition rule (400), as fixed in ARCHITECTURE.md section 5.</summary>
    public static StatusChangeCheck Classify(Request current, byte[] expectedVersion, RequestStatus target)
    {
        if (!current.RowVersion.AsSpan().SequenceEqual(expectedVersion))
        {
            return StatusChangeCheck.VersionMismatch;
        }

        return StatusTransitionPolicy.IsAllowed(current.Status, target)
            ? StatusChangeCheck.Allowed
            : StatusChangeCheck.TransitionNotAllowed;
    }
}
