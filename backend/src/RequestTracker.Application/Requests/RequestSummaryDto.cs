using RequestTracker.Domain;

namespace RequestTracker.Application.Requests;

public sealed record StatusCountDto(RequestStatus Status, int Count);

public sealed record PriorityCountDto(RequestPriority Priority, int Count);

/// <summary>Global counts. <paramref name="GeneratedAtUtc"/> is when they were computed, so staleness is visible.</summary>
public sealed record RequestSummaryDto(
    int TotalCount,
    IReadOnlyList<StatusCountDto> ByStatus,
    IReadOnlyList<PriorityCountDto> ByPriority,
    DateTime GeneratedAtUtc);
