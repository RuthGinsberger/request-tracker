using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RequestTracker.Application.Abstractions;
using RequestTracker.Domain;

namespace RequestTracker.Application.Requests;

/// <summary>
/// The one implementation of "change the status of a loaded request", used by the single update and by the bulk
/// update. It never invalidates caches: the caller does that once, after its own successful saves.
/// </summary>
public sealed class RequestStatusChanger(IAppDbContext db, TimeProvider timeProvider, ILogger<RequestStatusChanger> logger)
{
    /// <param name="request">A tracked entity just loaded from the database.</param>
    /// <param name="expectedVersion">The version the client last saw.</param>
    public async Task<StatusChangeOutcome> ApplyAsync(
        Request request, byte[] expectedVersion, RequestStatus target, string userName, CancellationToken cancellationToken)
    {
        var requestId = request.Id;
        var from = request.Status;

        switch (StatusChangeClassifier.Classify(request, expectedVersion, target))
        {
            case StatusChangeCheck.VersionMismatch:
                // Fast path: the version is already stale, so no write is attempted.
                logger.LogWarning("Status change of request {RequestId} rejected: stale version", requestId);
                return new StatusChangeOutcome.Conflict(request.Status, VersionToken.Encode(request.RowVersion));
            case StatusChangeCheck.TransitionNotAllowed:
                return new StatusChangeOutcome.InvalidTransition(request.Status, target);
        }

        if (request.ChangeStatus(target, userName, timeProvider.GetUtcNow().UtcDateTime) is not StatusChangeResult.Changed changed)
        {
            return new StatusChangeOutcome.InvalidTransition(from, target); // unreachable after Classify; keeps the switch total
        }

        // The decisive guard: UPDATE ... WHERE Id = @id AND RowVersion = @expected. Without this line EF would compare
        // against the version it just read, which could already include another writer's commit.
        db.Entry(request).Property(r => r.RowVersion).OriginalValue = expectedVersion;
        db.StatusHistory.Add(changed.History);

        try
        {
            // One SaveChanges = one implicit transaction: the status change and its audit row commit or roll back together.
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Someone committed between our read and our write: the UPDATE matched 0 rows and rolled the audit row back.
            logger.LogWarning("Status change of request {RequestId} rejected: concurrent modification", requestId);
            return await ConflictAfterRaceAsync(request, changed.History, cancellationToken);
        }

        logger.LogInformation(
            "Request {RequestId} status changed from {PreviousStatus} to {NewStatus}", requestId, from, target);
        return new StatusChangeOutcome.Updated(request);
    }

    private async Task<StatusChangeOutcome> ConflictAfterRaceAsync(
        Request request, RequestStatusHistory history, CancellationToken cancellationToken)
    {
        var entry = db.Entry(request);
        var current = await entry.GetDatabaseValuesAsync(cancellationToken);

        // Forget the failed attempt so the context (bulk reuses it) does not retry it on the next save.
        db.Entry(history).State = EntityState.Detached;
        entry.State = EntityState.Detached;

        if (current is null)
        {
            return new StatusChangeOutcome.NotFound();
        }

        return new StatusChangeOutcome.Conflict(
            current.GetValue<RequestStatus>(nameof(Request.Status)),
            VersionToken.Encode(current.GetValue<byte[]>(nameof(Request.RowVersion))));
    }
}
