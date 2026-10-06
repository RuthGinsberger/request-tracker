using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RequestTracker.Application.Abstractions;
using RequestTracker.Application.Caching;
using RequestTracker.Application.Requests;

namespace RequestTracker.Infrastructure.Caching;

/// <summary>
/// <see cref="IRequestReadCache"/> over the in-process HybridCache (stampede-protected: concurrent misses run the
/// query once). The HybridCache is a singleton; this wrapper is scoped because the factories use the scoped DbContext.
/// </summary>
public sealed class RequestReadCache(
    HybridCache cache,
    RequestSummaryService queries,
    IOptions<CacheOptions> options,
    ILogger<RequestReadCache> logger) : IRequestReadCache
{
    public const string SummaryKey = "requests:summary:v1";
    public const string LookupsKey = "requests:lookups:v1";
    public const string SummaryTag = "summary";
    public const string LookupsTag = "lookups";

    private static readonly string[] SummaryTags = [SummaryTag];
    private static readonly string[] LookupsTags = [LookupsTag];

    public async Task<RequestSummaryDto> GetSummaryAsync(CancellationToken cancellationToken) =>
        await cache.GetOrCreateAsync(
            SummaryKey,
            async token =>
            {
                logger.LogDebug("Cache miss for {CacheKey}; computing the summary", SummaryKey);
                return await queries.GetSummaryAsync(token);
            },
            Expire(options.Value.SummaryTtl),
            SummaryTags,
            cancellationToken);

    public async Task<LookupsDto> GetLookupsAsync(CancellationToken cancellationToken) =>
        await cache.GetOrCreateAsync(
            LookupsKey,
            async token =>
            {
                logger.LogDebug("Cache miss for {CacheKey}; computing the lookups", LookupsKey);
                return await queries.GetLookupsAsync(token);
            },
            Expire(options.Value.LookupsTtl),
            LookupsTags,
            cancellationToken);

    public async Task InvalidateSummaryAsync(CancellationToken cancellationToken)
    {
        await cache.RemoveByTagAsync(SummaryTag, cancellationToken);
        logger.LogInformation("Invalidated the cached summary");
    }

    // Absolute expiration for both cache levels (there is only the in-process one today).
    private static HybridCacheEntryOptions Expire(TimeSpan ttl) => new() { Expiration = ttl, LocalCacheExpiration = ttl };
}
