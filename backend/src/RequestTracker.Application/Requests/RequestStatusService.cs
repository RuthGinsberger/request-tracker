using Microsoft.EntityFrameworkCore;
using RequestTracker.Application.Abstractions;
using RequestTracker.Domain;

namespace RequestTracker.Application.Requests;

public sealed class RequestStatusService(IAppDbContext db, RequestStatusChanger changer, IRequestReadCache cache)
{
    /// <summary>
    /// Input (id, status, user name, version format) is assumed valid. Order: 404, then 409 (version), then 400 (transition).
    /// </summary>
    public async Task<ChangeStatusResult> ChangeStatusAsync(
        int id, RequestStatus target, byte[] expectedVersion, string userName, CancellationToken cancellationToken)
    {
        // Tracked on purpose: the change is applied to this entity and saved with one SaveChanges.
        var request = await db.Requests.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (request is null)
        {
            return new ChangeStatusResult.NotFound();
        }

        var outcome = await changer.ApplyAsync(request, expectedVersion, target, userName, cancellationToken);
        switch (outcome)
        {
            case StatusChangeOutcome.Updated updated:
                // Only after a successful commit (never before, never for a rejected or rolled-back change).
                await cache.InvalidateSummaryAsync(cancellationToken);
                return new ChangeStatusResult.Updated(RequestMappings.ToDetailsDto(updated.Request));
            case StatusChangeOutcome.Conflict conflict:
                return new ChangeStatusResult.Conflict(conflict.CurrentStatus, conflict.CurrentVersion);
            case StatusChangeOutcome.InvalidTransition invalid:
                return new ChangeStatusResult.InvalidTransition(invalid.CurrentStatus, invalid.RequestedStatus);
            default:
                return new ChangeStatusResult.NotFound();
        }
    }

    /// <summary>Newest first; ties broken by Id so the order is total.</summary>
    public async Task<HistoryResult> GetHistoryAsync(int id, CancellationToken cancellationToken)
    {
        if (!await db.Requests.AnyAsync(r => r.Id == id, cancellationToken))
        {
            return new HistoryResult.NotFound();
        }

        var items = await db.StatusHistory
            .AsNoTracking()
            .Where(h => h.RequestId == id)
            .OrderByDescending(h => h.ChangedAt)
            .ThenByDescending(h => h.Id)
            .Select(StatusHistoryItemDto.Projection)
            .ToListAsync(cancellationToken);

        return new HistoryResult.Found(items);
    }
}
