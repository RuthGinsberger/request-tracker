using RequestTracker.Application.Requests;

namespace RequestTracker.Application.Abstractions;

/// <summary>
/// Cached read scenarios (summary and lookups). Update paths call <see cref="InvalidateSummaryAsync"/> only after the
/// change is committed. Lookups are never invalidated: no endpoint changes organizations or assignees (A10).
/// </summary>
public interface IRequestReadCache
{
    Task<RequestSummaryDto> GetSummaryAsync(CancellationToken cancellationToken);

    Task<LookupsDto> GetLookupsAsync(CancellationToken cancellationToken);

    Task InvalidateSummaryAsync(CancellationToken cancellationToken);
}
