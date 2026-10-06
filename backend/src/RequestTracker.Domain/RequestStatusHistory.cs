namespace RequestTracker.Domain;

/// <summary>Audit record of one status change (R20). Created only by <see cref="Request.ChangeStatus"/>.</summary>
public class RequestStatusHistory
{
    public const int ChangedByMaxLength = 100;

    // For EF Core.
    private RequestStatusHistory()
    {
        ChangedBy = string.Empty;
    }

    internal RequestStatusHistory(
        int requestId, RequestStatus previousStatus, RequestStatus newStatus, DateTime changedAt, string changedBy)
    {
        RequestId = requestId;
        PreviousStatus = previousStatus;
        NewStatus = newStatus;
        ChangedAt = changedAt;
        ChangedBy = changedBy;
    }

    public long Id { get; private set; }

    public int RequestId { get; private set; }

    public RequestStatus PreviousStatus { get; private set; }

    public RequestStatus NewStatus { get; private set; }

    public DateTime ChangedAt { get; private set; }

    public string ChangedBy { get; private set; }
}
