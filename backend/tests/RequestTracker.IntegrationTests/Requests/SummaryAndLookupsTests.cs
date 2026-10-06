using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RequestTracker.Application.Abstractions;
using RequestTracker.Application.Requests;
using RequestTracker.Domain;
using RequestTracker.IntegrationTests.Infrastructure;

namespace RequestTracker.IntegrationTests.Requests;

public sealed class SummaryAndLookupsTests(SqlServerFixture fixture) : IntegrationTestBase(fixture)
{
    private async Task<RequestSummaryDto> GetSummaryAsync()
    {
        var response = await Client.GetAsync("/api/requests/summary");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<RequestSummaryDto>(Json.Options))!;
    }

    private async Task<LookupsDto> GetLookupsAsync()
    {
        var response = await Client.GetAsync("/api/lookups");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<LookupsDto>(Json.Options))!;
    }

    [Fact]
    public async Task Summary_counts_by_status_and_priority_with_zeros_for_missing_members()
    {
        await Data.AddRequestAsync(status: RequestStatus.New, priority: RequestPriority.High);
        await Data.AddRequestAsync(status: RequestStatus.New, priority: RequestPriority.Low);
        await Data.AddRequestAsync(status: RequestStatus.Waiting, priority: RequestPriority.High);

        var summary = await GetSummaryAsync();

        Assert.Equal(3, summary.TotalCount);
        Assert.Equal(
            [(RequestStatus.New, 2), (RequestStatus.InProgress, 0), (RequestStatus.Waiting, 1), (RequestStatus.Completed, 0)],
            summary.ByStatus.Select(s => (s.Status, s.Count)));
        Assert.Equal(
            [(RequestPriority.Low, 1), (RequestPriority.Medium, 0), (RequestPriority.High, 2)],
            summary.ByPriority.Select(p => (p.Priority, p.Count)));
        Assert.Equal(DateTimeKind.Utc, summary.GeneratedAtUtc.Kind);
        Assert.InRange(summary.GeneratedAtUtc, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1));
    }

    [Fact]
    public async Task Summary_of_an_empty_database_is_all_zeros()
    {
        var summary = await GetSummaryAsync();

        Assert.Equal(0, summary.TotalCount);
        Assert.Equal(4, summary.ByStatus.Count);
        Assert.Equal(3, summary.ByPriority.Count);
        Assert.All(summary.ByStatus, s => Assert.Equal(0, s.Count));
    }

    [Fact]
    public async Task Summary_json_uses_string_enums_and_camelCase()
    {
        await Data.AddRequestAsync();

        var json = await Client.GetStringAsync("/api/requests/summary");

        Assert.Contains("\"totalCount\":1", json);
        Assert.Contains("\"status\":\"New\"", json);
        Assert.Contains("\"priority\":\"Medium\"", json);
        Assert.Contains("\"generatedAtUtc\"", json);
    }

    [Fact]
    public async Task Second_summary_call_is_served_from_cache_until_invalidated()
    {
        await Data.AddRequestAsync();
        var first = await GetSummaryAsync();
        Assert.Equal(1, first.TotalCount);

        // A row inserted straight into the database is invisible while the cached value is live.
        await Data.AddRequestAsync();
        var cached = await GetSummaryAsync();
        Assert.Equal(1, cached.TotalCount);
        Assert.Equal(first.GeneratedAtUtc, cached.GeneratedAtUtc);

        // After explicit invalidation the next call recomputes.
        await using (var scope = Fixture.Factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IRequestReadCache>().InvalidateSummaryAsync(CancellationToken.None);
        }

        var fresh = await GetSummaryAsync();
        Assert.Equal(2, fresh.TotalCount);
        Assert.True(fresh.GeneratedAtUtc > first.GeneratedAtUtc);
    }

    [Fact]
    public async Task Lookups_are_distinct_sorted_and_exclude_unassigned()
    {
        await Data.AddRequestAsync(organizationName: "Globex", assignedTo: "noa");
        await Data.AddRequestAsync(organizationName: "acme", assignedTo: "Dana");
        await Data.AddRequestAsync(organizationName: "Globex", assignedTo: "noa");
        await Data.AddRequestAsync(organizationName: "Initech", assignedTo: null);

        var lookups = await GetLookupsAsync();

        Assert.Equal(["acme", "Globex", "Initech"], lookups.Organizations);
        Assert.Equal(["Dana", "noa"], lookups.Assignees);
    }

    [Fact]
    public async Task Lookups_of_an_empty_database_are_empty_and_are_cached_without_invalidation()
    {
        var empty = await GetLookupsAsync();
        Assert.Empty(empty.Organizations);
        Assert.Empty(empty.Assignees);

        await Data.AddRequestAsync(organizationName: "Globex");

        var cached = await GetLookupsAsync();
        Assert.Empty(cached.Organizations); // lookups expire by TTL only
    }

    [Fact]
    public async Task Summary_route_does_not_collide_with_get_by_id()
    {
        var created = await Data.AddRequestAsync();

        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync("/api/requests/summary")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync($"/api/requests/{created.Id}")).StatusCode);
    }
}
