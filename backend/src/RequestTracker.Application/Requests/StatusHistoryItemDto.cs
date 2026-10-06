using System.Linq.Expressions;
using RequestTracker.Domain;

namespace RequestTracker.Application.Requests;

/// <summary>One audit entry of a request's status changes (R20, R21).</summary>
public sealed record StatusHistoryItemDto(
    long Id,
    int RequestId,
    RequestStatus PreviousStatus,
    RequestStatus NewStatus,
    DateTime ChangedAt,
    string ChangedBy)
{
    public static readonly Expression<Func<RequestStatusHistory, StatusHistoryItemDto>> Projection = h => new StatusHistoryItemDto(
        h.Id, h.RequestId, h.PreviousStatus, h.NewStatus, h.ChangedAt, h.ChangedBy);
}

public abstract record HistoryResult
{
    private HistoryResult()
    {
    }

    public sealed record Found(IReadOnlyList<StatusHistoryItemDto> Items) : HistoryResult;

    public sealed record NotFound : HistoryResult;
}
