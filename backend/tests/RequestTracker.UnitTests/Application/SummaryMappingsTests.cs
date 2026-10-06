using RequestTracker.Application.Caching;
using RequestTracker.Application.Requests;
using RequestTracker.Domain;

namespace RequestTracker.UnitTests.Application;

public sealed class SummaryMappingsTests
{
    private static readonly DateTime Now = new(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Missing_enum_members_are_filled_with_zero_in_enum_order()
    {
        var summary = SummaryMappings.ToSummary(
            new Dictionary<RequestStatus, int> { [RequestStatus.Waiting] = 3, [RequestStatus.New] = 2 },
            new Dictionary<RequestPriority, int> { [RequestPriority.High] = 5 },
            Now);

        Assert.Equal(
            [(RequestStatus.New, 2), (RequestStatus.InProgress, 0), (RequestStatus.Waiting, 3), (RequestStatus.Completed, 0)],
            summary.ByStatus.Select(s => (s.Status, s.Count)));
        Assert.Equal(
            [(RequestPriority.Low, 0), (RequestPriority.Medium, 0), (RequestPriority.High, 5)],
            summary.ByPriority.Select(p => (p.Priority, p.Count)));
    }

    [Fact]
    public void Total_is_the_sum_of_the_status_counts_and_the_timestamp_is_kept()
    {
        var summary = SummaryMappings.ToSummary(
            new Dictionary<RequestStatus, int> { [RequestStatus.New] = 2, [RequestStatus.Completed] = 7 },
            new Dictionary<RequestPriority, int>(),
            Now);

        Assert.Equal(9, summary.TotalCount);
        Assert.Equal(Now, summary.GeneratedAtUtc);
    }

    [Fact]
    public void Empty_input_gives_all_zeros()
    {
        var summary = SummaryMappings.ToSummary(
            new Dictionary<RequestStatus, int>(), new Dictionary<RequestPriority, int>(), Now);

        Assert.Equal(0, summary.TotalCount);
        Assert.Equal(4, summary.ByStatus.Count);
        Assert.Equal(3, summary.ByPriority.Count);
        Assert.All(summary.ByStatus, s => Assert.Equal(0, s.Count));
        Assert.All(summary.ByPriority, p => Assert.Equal(0, p.Count));
    }

    [Theory]
    [InlineData(60, 10, true)]
    [InlineData(1, 1, true)]
    [InlineData(0, 10, false)]
    [InlineData(60, 0, false)]
    [InlineData(-5, 10, false)]
    [InlineData(60, 100_000, false)]
    public void Cache_options_require_positive_bounded_expirations(int summarySeconds, int lookupsMinutes, bool valid)
    {
        var result = new CacheOptionsValidator().Validate(
            null, new CacheOptions { SummaryTtlSeconds = summarySeconds, LookupsTtlMinutes = lookupsMinutes });

        Assert.Equal(valid, result.Succeeded);
    }

    [Fact]
    public void Default_expirations_are_60_seconds_and_10_minutes()
    {
        var options = new CacheOptions();

        Assert.Equal(TimeSpan.FromSeconds(60), options.SummaryTtl);
        Assert.Equal(TimeSpan.FromMinutes(10), options.LookupsTtl);
    }
}
