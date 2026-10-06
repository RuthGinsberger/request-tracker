using Microsoft.EntityFrameworkCore;
using RequestTracker.Application.Abstractions;

namespace RequestTracker.Application.Requests;

/// <summary>Computes the summary and lookups in SQL (GROUP BY / DISTINCT); never materializes request rows.</summary>
public sealed class RequestSummaryService(IAppDbContext db, TimeProvider timeProvider)
{
    public async Task<RequestSummaryDto> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var statusCounts = await db.Requests.AsNoTracking()
            .GroupBy(r => r.Status)
            .Select(g => new { Key = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

        var priorityCounts = await db.Requests.AsNoTracking()
            .GroupBy(r => r.Priority)
            .Select(g => new { Key = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

        return SummaryMappings.ToSummary(statusCounts, priorityCounts, timeProvider.GetUtcNow().UtcDateTime);
    }

    public async Task<LookupsDto> GetLookupsAsync(CancellationToken cancellationToken)
    {
        var organizations = await db.Requests.AsNoTracking()
            .Select(r => r.OrganizationName)
            .Distinct()
            .ToListAsync(cancellationToken);

        var assignees = await db.Requests.AsNoTracking()
            .Where(r => r.AssignedTo != null)
            .Select(r => r.AssignedTo!)
            .Distinct()
            .ToListAsync(cancellationToken);

        // Sorted here so the order is the documented ordinal-ignore-case, whatever the database collation is.
        return new LookupsDto(
            [.. organizations.Order(StringComparer.OrdinalIgnoreCase)],
            [.. assignees.Order(StringComparer.OrdinalIgnoreCase)]);
    }
}
