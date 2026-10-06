namespace RequestTracker.Application.Caching;

/// <summary>Absolute expirations for the read cache, bound from the <c>Cache</c> configuration section.</summary>
public sealed class CacheOptions
{
    public const string SectionName = "Cache";

    public int SummaryTtlSeconds { get; set; } = 60;

    public int LookupsTtlMinutes { get; set; } = 10;

    public TimeSpan SummaryTtl => TimeSpan.FromSeconds(SummaryTtlSeconds);

    public TimeSpan LookupsTtl => TimeSpan.FromMinutes(LookupsTtlMinutes);
}
