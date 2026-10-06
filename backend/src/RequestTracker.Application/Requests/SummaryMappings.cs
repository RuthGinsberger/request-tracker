using RequestTracker.Domain;

namespace RequestTracker.Application.Requests;

/// <summary>Turns the sparse GROUP BY rows into the DTO: every enum member present (0 when absent), in enum order.</summary>
public static class SummaryMappings
{
    public static RequestSummaryDto ToSummary(
        IReadOnlyDictionary<RequestStatus, int> statusCounts,
        IReadOnlyDictionary<RequestPriority, int> priorityCounts,
        DateTime generatedAtUtc)
    {
        var byStatus = Enum.GetValues<RequestStatus>()
            .Select(s => new StatusCountDto(s, statusCounts.GetValueOrDefault(s)))
            .ToList();
        var byPriority = Enum.GetValues<RequestPriority>()
            .Select(p => new PriorityCountDto(p, priorityCounts.GetValueOrDefault(p)))
            .ToList();

        // The total is the sum of the status counts, so the two always agree.
        return new RequestSummaryDto(byStatus.Sum(s => s.Count), byStatus, byPriority, generatedAtUtc);
    }
}
