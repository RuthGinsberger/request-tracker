using Microsoft.Extensions.Options;

namespace RequestTracker.Application.Caching;

/// <summary>Fails startup when an expiration is not positive (a zero or negative TTL would silently disable caching).</summary>
public sealed class CacheOptionsValidator : IValidateOptions<CacheOptions>
{
    public ValidateOptionsResult Validate(string? name, CacheOptions options)
    {
        var failures = new List<string>();
        if (options.SummaryTtlSeconds is < 1 or > 86_400)
        {
            failures.Add($"{CacheOptions.SectionName}:{nameof(CacheOptions.SummaryTtlSeconds)} must be between 1 and 86400.");
        }

        if (options.LookupsTtlMinutes is < 1 or > 1_440)
        {
            failures.Add($"{CacheOptions.SectionName}:{nameof(CacheOptions.LookupsTtlMinutes)} must be between 1 and 1440.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
