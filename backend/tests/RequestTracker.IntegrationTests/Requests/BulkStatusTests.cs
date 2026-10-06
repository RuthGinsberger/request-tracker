using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RequestTracker.Application;
using RequestTracker.Application.Requests;
using RequestTracker.Domain;
using RequestTracker.IntegrationTests.Infrastructure;

namespace RequestTracker.IntegrationTests.Requests;

public sealed class BulkStatusTests(SqlServerFixture fixture) : IntegrationTestBase(fixture)
{
    private Task<HttpResponseMessage> PostAsync(string body, string? user = "dana")
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/requests/bulk-status")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        if (user is not null)
        {
            message.Headers.TryAddWithoutValidation("X-User-Name", user);
        }

        return Client.SendAsync(message);
    }

    private static string Item(int id, byte[] version) => $"{{\"id\":{id},\"version\":\"{VersionToken.Encode(version)}\"}}";

    private static string BodyOf(RequestStatus target, params string[] items) =>
        $"{{\"targetStatus\":\"{target}\",\"items\":[{string.Join(",", items)}]}}";

    private static async Task<BulkStatusResultDto> ReadAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<BulkStatusResultDto>(Json.Options))!;
    }

    private async Task<List<RequestStatusHistory>> HistoryAsync()
    {
        await using var db = Fixture.CreateDbContext();
        return await db.StatusHistory.AsNoTracking().ToListAsync();
    }

    private async Task<Request> ReloadAsync(int id)
    {
        await using var db = Fixture.CreateDbContext();
        return await db.Requests.AsNoTracking().SingleAsync(r => r.Id == id);
    }

    private async Task<RequestSummaryDto> SummaryAsync() =>
        (await Client.GetFromJsonAsync<RequestSummaryDto>("/api/requests/summary", Json.Options))!;

    [Fact]
    public async Task Mixed_batch_reports_exact_outcomes_in_request_order()
    {
        var ok1 = await Data.AddRequestAsync(status: RequestStatus.InProgress);
        var stale = await Data.AddRequestAsync(status: RequestStatus.InProgress);
        var notAllowed = await Data.AddRequestAsync(status: RequestStatus.New); // New -> Completed is not allowed
        var ok2 = await Data.AddRequestAsync(status: RequestStatus.InProgress);
        const int missingId = 987_654;

        // A concurrent single update changes `stale` after the client read it.
        var concurrent = new HttpRequestMessage(HttpMethod.Patch, $"/api/requests/{stale.Id}/status")
        {
            Content = new StringContent("{\"status\":\"Completed\"}", Encoding.UTF8, "application/json"),
        };
        concurrent.Headers.TryAddWithoutValidation("If-Match", $"\"{VersionToken.Encode(stale.RowVersion)}\"");
        concurrent.Headers.TryAddWithoutValidation("X-User-Name", "someone-else");
        Assert.Equal(HttpStatusCode.OK, (await Client.SendAsync(concurrent)).StatusCode);

        var result = await ReadAsync(await PostAsync(BodyOf(
            RequestStatus.Completed,
            Item(ok1.Id, ok1.RowVersion),
            Item(missingId, ok1.RowVersion),
            Item(stale.Id, stale.RowVersion),
            Item(notAllowed.Id, notAllowed.RowVersion),
            Item(ok2.Id, ok2.RowVersion))));

        Assert.Equal(new BulkStatusSummaryDto(Requested: 5, Updated: 2, NotFound: 1, Conflict: 1, InvalidTransition: 1), result.Summary);
        Assert.Equal([ok1.Id, missingId, stale.Id, notAllowed.Id, ok2.Id], result.Results.Select(r => r.Id));
        Assert.Equal(
            [BulkItemOutcome.Updated, BulkItemOutcome.NotFound, BulkItemOutcome.Conflict, BulkItemOutcome.InvalidTransition, BulkItemOutcome.Updated],
            result.Results.Select(r => r.Outcome));

        // newVersion only for Updated, message only for failures.
        foreach (var item in result.Results)
        {
            Assert.Equal(item.IsUpdated, item.NewVersion is not null);
            Assert.Equal(!item.IsUpdated, item.Message is not null);
        }

        // New versions match the database; the rejected item keeps its row as it was.
        foreach (var updated in new[] { ok1, ok2 })
        {
            var stored = await ReloadAsync(updated.Id);
            Assert.Equal(RequestStatus.Completed, stored.Status);
            Assert.Equal(VersionToken.Encode(stored.RowVersion), result.Results.Single(r => r.Id == updated.Id).NewVersion);
        }

        Assert.Equal(RequestStatus.New, (await ReloadAsync(notAllowed.Id)).Status);
    }

    [Fact]
    public async Task Only_updated_items_get_history_rows()
    {
        var ok = await Data.AddRequestAsync(status: RequestStatus.InProgress);
        var notAllowed = await Data.AddRequestAsync(status: RequestStatus.New);
        var stale = await Data.AddRequestAsync(status: RequestStatus.InProgress);
        var staleVersion = stale.RowVersion;
        await using (var db = Fixture.CreateDbContext())
        {
            // Another writer touched the row after the client read it.
            await db.Requests.Where(r => r.Id == stale.Id).ExecuteUpdateAsync(s => s.SetProperty(r => r.AssignedTo, "x"));
        }

        var before = await ReloadAsync(notAllowed.Id);
        var result = await ReadAsync(await PostAsync(
            BodyOf(RequestStatus.Completed, Item(ok.Id, ok.RowVersion), Item(notAllowed.Id, notAllowed.RowVersion), Item(stale.Id, staleVersion)),
            "  noa "));

        Assert.Equal(1, result.Summary.Updated);
        var history = Assert.Single(await HistoryAsync());
        Assert.Equal(ok.Id, history.RequestId);
        Assert.Equal(RequestStatus.InProgress, history.PreviousStatus);
        Assert.Equal(RequestStatus.Completed, history.NewStatus);
        Assert.Equal("noa", history.ChangedBy);

        Assert.Equal(before.RowVersion, (await ReloadAsync(notAllowed.Id)).RowVersion);
        Assert.Equal(RequestStatus.InProgress, (await ReloadAsync(stale.Id)).Status);
    }

    [Fact]
    public async Task All_items_failing_is_still_200()
    {
        var notAllowed = await Data.AddRequestAsync(status: RequestStatus.New);

        var result = await ReadAsync(await PostAsync(BodyOf(
            RequestStatus.Completed, Item(notAllowed.Id, notAllowed.RowVersion), Item(424_242, notAllowed.RowVersion))));

        Assert.Equal(new BulkStatusSummaryDto(2, 0, 1, 0, 1), result.Summary);
        Assert.Empty(await HistoryAsync());
    }

    [Fact]
    public async Task Outcomes_use_camel_case_names_on_the_wire()
    {
        var created = await Data.AddRequestAsync(status: RequestStatus.InProgress);

        var response = await PostAsync(BodyOf(
            RequestStatus.Completed, Item(created.Id, created.RowVersion), Item(777_777, created.RowVersion)));

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("updated", json.GetProperty("results")[0].GetProperty("outcome").GetString());
        Assert.Equal("notFound", json.GetProperty("results")[1].GetProperty("outcome").GetString());
        Assert.Equal(2, json.GetProperty("summary").GetProperty("requested").GetInt32());
    }

    [Fact]
    public async Task More_than_100_items_is_400_and_nothing_is_processed()
    {
        var created = await Data.AddRequestAsync(status: RequestStatus.InProgress);
        var items = new[] { Item(created.Id, created.RowVersion) }
            .Concat(Enumerable.Range(1_000_000, 100).Select(i => Item(i, created.RowVersion))).ToArray(); // 101 items

        var response = await PostAsync(BodyOf(RequestStatus.Completed, items));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(RequestStatus.InProgress, (await ReloadAsync(created.Id)).Status);
        Assert.Empty(await HistoryAsync());
    }

    [Fact]
    public async Task Exactly_100_items_is_accepted()
    {
        var created = await Data.AddRequestAsync(status: RequestStatus.InProgress);
        var items = new[] { Item(created.Id, created.RowVersion) }
            .Concat(Enumerable.Range(1_000_000, 99).Select(i => Item(i, created.RowVersion))).ToArray();

        var result = await ReadAsync(await PostAsync(BodyOf(RequestStatus.Completed, items)));

        Assert.Equal(new BulkStatusSummaryDto(100, 1, 99, 0, 0), result.Summary);
    }

    [Fact]
    public async Task Duplicate_ids_are_400_and_nothing_is_processed()
    {
        var created = await Data.AddRequestAsync(status: RequestStatus.InProgress);

        var response = await PostAsync(BodyOf(
            RequestStatus.Completed, Item(created.Id, created.RowVersion), Item(created.Id, created.RowVersion)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(RequestStatus.InProgress, (await ReloadAsync(created.Id)).Status);
        Assert.Empty(await HistoryAsync());
    }

    [Theory]
    [InlineData("{\"targetStatus\":\"Completed\",\"items\":[]}")]
    [InlineData("{\"targetStatus\":\"Completed\"}")]
    [InlineData("{\"targetStatus\":\"Nope\",\"items\":[{\"id\":1,\"version\":\"AAAAAAAAB9E=\"}]}")]
    [InlineData("{\"targetStatus\":\"Completed\",\"items\":[{\"id\":1,\"version\":\"bad\"}]}")]
    public async Task Invalid_bodies_are_400(string body)
    {
        var response = await PostAsync(body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Missing_user_header_is_400_and_nothing_is_processed()
    {
        var created = await Data.AddRequestAsync(status: RequestStatus.InProgress);

        var response = await PostAsync(BodyOf(RequestStatus.Completed, Item(created.Id, created.RowVersion)), user: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(RequestStatus.InProgress, (await ReloadAsync(created.Id)).Status);
    }

    [Fact]
    public async Task Successful_bulk_invalidates_the_summary_cache()
    {
        var created = await Data.AddRequestAsync(status: RequestStatus.InProgress);
        var cached = await SummaryAsync();

        await ReadAsync(await PostAsync(BodyOf(RequestStatus.Completed, Item(created.Id, created.RowVersion))));

        var fresh = await SummaryAsync();
        Assert.Equal(0, fresh.ByStatus.Single(s => s.Status == RequestStatus.InProgress).Count);
        Assert.Equal(1, fresh.ByStatus.Single(s => s.Status == RequestStatus.Completed).Count);
        Assert.True(fresh.GeneratedAtUtc > cached.GeneratedAtUtc);
    }

    [Fact]
    public async Task Bulk_without_any_update_does_not_invalidate_the_cache()
    {
        var created = await Data.AddRequestAsync(status: RequestStatus.New);
        var cached = await SummaryAsync();

        await ReadAsync(await PostAsync(BodyOf(RequestStatus.Completed, Item(created.Id, created.RowVersion))));

        Assert.Equal(cached.GeneratedAtUtc, (await SummaryAsync()).GeneratedAtUtc);
    }
}
