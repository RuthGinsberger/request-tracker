using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RequestTracker.Application.Abstractions;
using RequestTracker.Domain;

namespace RequestTracker.Application.Requests;

/// <summary>
/// Bulk status update with Partial Success (R22, R23): every item gets its own outcome and its own transaction.
/// Classification and application are delegated to <see cref="RequestStatusChanger"/>, the same code the single update uses.
/// </summary>
public sealed class BulkStatusService(
    IAppDbContext db, RequestStatusChanger changer, IRequestReadCache cache, ILogger<BulkStatusService> logger)
{
    /// <summary>
    /// Input (1-100 items, unique ids, valid versions, user name) is assumed valid. Results are in request order.
    /// If <paramref name="cancellationToken"/> is cancelled, processing stops and the exception propagates; items already
    /// saved stay committed (Partial Success), and the summary cache is not invalidated by this call, so it can serve
    /// counts up to one TTL old (a documented limitation).
    /// </summary>
    public async Task<BulkStatusResultDto> ChangeStatusAsync(
        RequestStatus target,
        IReadOnlyList<(int Id, byte[] Version)> items,
        string userName,
        CancellationToken cancellationToken)
    {
        var ids = items.Select(i => i.Id).ToList();

        // One query for the whole batch, tracked: each found entity is changed and saved below.
        var loaded = await db.Requests
            .Where(r => ids.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, cancellationToken);

        var results = new List<BulkItemResultDto>(items.Count);
        foreach (var (id, version) in items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!loaded.TryGetValue(id, out var request))
            {
                results.Add(NotFound(id));
                continue;
            }

            var outcome = await changer.ApplyAsync(request, version, target, userName, cancellationToken);
            results.Add(outcome switch
            {
                StatusChangeOutcome.Updated updated => new BulkItemResultDto(
                    id, BulkItemOutcome.Updated, VersionToken.Encode(updated.Request.RowVersion), null),
                StatusChangeOutcome.Conflict => new BulkItemResultDto(
                    id, BulkItemOutcome.Conflict, null, $"Request {id} was modified by someone else."),
                StatusChangeOutcome.InvalidTransition invalid => new BulkItemResultDto(
                    id, BulkItemOutcome.InvalidTransition, null,
                    $"Request {id} cannot move from {invalid.CurrentStatus} to {invalid.RequestedStatus}."),
                _ => NotFound(id),
            });
        }

        var summary = Summarize(results);
        if (summary.Updated > 0)
        {
            // Once per call, after every save has finished.
            await cache.InvalidateSummaryAsync(cancellationToken);
        }

        logger.LogInformation(
            "Bulk status change to {TargetStatus}: {Requested} requested, {Updated} updated, {NotFound} not found, "
            + "{Conflict} conflicts, {InvalidTransition} invalid transitions",
            target, summary.Requested, summary.Updated, summary.NotFound, summary.Conflict, summary.InvalidTransition);
        if (summary.Conflict > 0)
        {
            logger.LogWarning("Bulk status change had {Conflict} version conflicts", summary.Conflict);
        }

        return new BulkStatusResultDto(summary, results);
    }

    private static BulkItemResultDto NotFound(int id) =>
        new(id, BulkItemOutcome.NotFound, null, $"Request {id} does not exist.");

    private static BulkStatusSummaryDto Summarize(List<BulkItemResultDto> results)
    {
        int Count(BulkItemOutcome outcome) => results.Count(r => r.Outcome == outcome);
        return new BulkStatusSummaryDto(
            results.Count,
            Count(BulkItemOutcome.Updated),
            Count(BulkItemOutcome.NotFound),
            Count(BulkItemOutcome.Conflict),
            Count(BulkItemOutcome.InvalidTransition));
    }
}
