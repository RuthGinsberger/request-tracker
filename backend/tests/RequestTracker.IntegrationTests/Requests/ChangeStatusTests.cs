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

public sealed class ChangeStatusTests(SqlServerFixture fixture) : IntegrationTestBase(fixture)
{
    private static string Quoted(byte[] version) => $"\"{VersionToken.Encode(version)}\"";

    private Task<HttpResponseMessage> PatchAsync(
        object id, string? body, string? ifMatch, string? user = "dana", HttpClient? client = null)
    {
        var message = new HttpRequestMessage(HttpMethod.Patch, $"/api/requests/{id}/status")
        {
            Content = new StringContent(body ?? string.Empty, Encoding.UTF8, "application/json"),
        };
        if (ifMatch is not null)
        {
            message.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        if (user is not null)
        {
            message.Headers.TryAddWithoutValidation("X-User-Name", user);
        }

        return (client ?? Client).SendAsync(message);
    }

    private static string StatusBody(RequestStatus status) => $"{{\"status\":\"{status}\"}}";

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private async Task<List<RequestStatusHistory>> HistoryRowsAsync(int requestId)
    {
        await using var db = Fixture.CreateDbContext();
        return await db.StatusHistory.AsNoTracking().Where(h => h.RequestId == requestId).ToListAsync();
    }

    private async Task<Request> ReloadAsync(int id)
    {
        await using var db = Fixture.CreateDbContext();
        return await db.Requests.AsNoTracking().SingleAsync(r => r.Id == id);
    }

    private async Task<RequestSummaryDto> SummaryAsync() =>
        (await Client.GetFromJsonAsync<RequestSummaryDto>("/api/requests/summary", Json.Options))!;

    [Fact]
    public async Task Happy_path_updates_status_version_and_etag_and_writes_one_history_row()
    {
        var created = await Data.AddRequestAsync(status: RequestStatus.New);
        var before = DateTime.UtcNow.AddSeconds(-5);

        var response = await PatchAsync(created.Id, StatusBody(RequestStatus.InProgress), Quoted(created.RowVersion), "  noa  ");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = (await response.Content.ReadFromJsonAsync<RequestDetailsDto>(Json.Options))!;
        Assert.Equal(RequestStatus.InProgress, dto.Status);
        Assert.NotEqual(VersionToken.Encode(created.RowVersion), dto.Version);
        Assert.True(dto.UpdatedAt > created.UpdatedAt);
        Assert.InRange(dto.UpdatedAt, before, DateTime.UtcNow.AddSeconds(5));
        Assert.Equal($"\"{dto.Version}\"", response.Headers.ETag?.ToString());

        // The response matches what is stored.
        var stored = await ReloadAsync(created.Id);
        Assert.Equal(RequestStatus.InProgress, stored.Status);
        Assert.Equal(dto.Version, VersionToken.Encode(stored.RowVersion));
        Assert.Equal(dto.UpdatedAt, stored.UpdatedAt);

        var history = Assert.Single(await HistoryRowsAsync(created.Id));
        Assert.Equal(RequestStatus.New, history.PreviousStatus);
        Assert.Equal(RequestStatus.InProgress, history.NewStatus);
        Assert.Equal("noa", history.ChangedBy); // trimmed
        Assert.Equal(stored.UpdatedAt, history.ChangedAt); // same instant as UpdatedAt
    }

    [Fact]
    public async Task The_returned_etag_can_be_used_for_the_next_change()
    {
        var created = await Data.AddRequestAsync(status: RequestStatus.New);
        var first = await PatchAsync(created.Id, StatusBody(RequestStatus.InProgress), Quoted(created.RowVersion));

        var second = await PatchAsync(created.Id, StatusBody(RequestStatus.Completed), first.Headers.ETag!.ToString());

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(2, (await HistoryRowsAsync(created.Id)).Count);
    }

    [Fact]
    public async Task Successful_update_invalidates_the_summary_cache()
    {
        var created = await Data.AddRequestAsync(status: RequestStatus.New);
        var cached = await SummaryAsync();
        Assert.Equal(1, cached.ByStatus.Single(s => s.Status == RequestStatus.New).Count);

        var response = await PatchAsync(created.Id, StatusBody(RequestStatus.InProgress), Quoted(created.RowVersion));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var fresh = await SummaryAsync();
        Assert.Equal(0, fresh.ByStatus.Single(s => s.Status == RequestStatus.New).Count);
        Assert.Equal(1, fresh.ByStatus.Single(s => s.Status == RequestStatus.InProgress).Count);
        Assert.True(fresh.GeneratedAtUtc > cached.GeneratedAtUtc);
    }

    [Fact]
    public async Task Rejected_update_does_not_invalidate_the_summary_cache()
    {
        var created = await Data.AddRequestAsync(status: RequestStatus.New);
        var cached = await SummaryAsync();

        var response = await PatchAsync(created.Id, StatusBody(RequestStatus.Completed), Quoted(created.RowVersion));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        Assert.Equal(cached.GeneratedAtUtc, (await SummaryAsync()).GeneratedAtUtc);
    }

    [Fact]
    public async Task Parallel_updates_with_the_same_version_let_exactly_one_win()
    {
        const int Parallelism = 10;
        var created = await Data.AddRequestAsync(status: RequestStatus.New);
        var etag = Quoted(created.RowVersion);
        using var gate = new ManualResetEventSlim(false);

        var calls = Enumerable.Range(0, Parallelism).Select(i => Task.Run(async () =>
        {
            gate.Wait();
            return await PatchAsync(created.Id, StatusBody(RequestStatus.InProgress), etag, $"user{i}", Fixture.Factory.CreateClient());
        })).ToArray();
        gate.Set();
        var responses = await Task.WhenAll(calls);

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(Parallelism - 1, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));

        var winner = await ReadJsonAsync(responses.Single(r => r.StatusCode == HttpStatusCode.OK));
        foreach (var loser in responses.Where(r => r.StatusCode == HttpStatusCode.Conflict))
        {
            var problem = await ReadJsonAsync(loser);
            Assert.Equal("version-conflict", problem.GetProperty("errorCode").GetString());
            Assert.Equal("InProgress", problem.GetProperty("currentStatus").GetString());
        }

        // One audit row, one version change, and the stored version is the winner's.
        var history = Assert.Single(await HistoryRowsAsync(created.Id));
        var stored = await ReloadAsync(created.Id);
        Assert.Equal(RequestStatus.InProgress, stored.Status);
        Assert.Equal(winner.GetProperty("version").GetString(), VersionToken.Encode(stored.RowVersion));
        Assert.StartsWith("user", history.ChangedBy);
    }

    [Fact]
    public async Task Parallel_updates_to_different_targets_still_let_exactly_one_win()
    {
        var created = await Data.AddRequestAsync(status: RequestStatus.InProgress);
        var etag = Quoted(created.RowVersion);
        using var gate = new ManualResetEventSlim(false);

        var calls = Enumerable.Range(0, 10).Select(i => Task.Run(async () =>
        {
            gate.Wait();
            var target = i % 2 == 0 ? RequestStatus.Waiting : RequestStatus.Completed;
            return await PatchAsync(created.Id, StatusBody(target), etag, "dana", Fixture.Factory.CreateClient());
        })).ToArray();
        gate.Set();
        var responses = await Task.WhenAll(calls);

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(9, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        Assert.Single(await HistoryRowsAsync(created.Id));
    }

    [Fact]
    public async Task Stale_version_returns_409_with_current_status_and_version_and_changes_nothing()
    {
        var created = await Data.AddRequestAsync(status: RequestStatus.New);
        var staleEtag = Quoted(created.RowVersion);
        var first = await PatchAsync(created.Id, StatusBody(RequestStatus.InProgress), staleEtag);
        var current = (await first.Content.ReadFromJsonAsync<RequestDetailsDto>(Json.Options))!;

        var second = await PatchAsync(created.Id, StatusBody(RequestStatus.Waiting), staleEtag);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("application/problem+json", second.Content.Headers.ContentType?.MediaType);
        var problem = await ReadJsonAsync(second);
        Assert.Equal(409, problem.GetProperty("status").GetInt32());
        Assert.Equal("version-conflict", problem.GetProperty("errorCode").GetString());
        Assert.Equal("urn:requesttracker:error:version-conflict", problem.GetProperty("type").GetString());
        Assert.Equal("InProgress", problem.GetProperty("currentStatus").GetString());
        Assert.Equal(current.Version, problem.GetProperty("currentVersion").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("traceId").GetString()));

        Assert.Single(await HistoryRowsAsync(created.Id));
        Assert.Equal(current.Version, VersionToken.Encode((await ReloadAsync(created.Id)).RowVersion));
    }

    [Fact]
    public async Task Version_conflict_is_reported_before_an_invalid_transition()
    {
        var created = await Data.AddRequestAsync(status: RequestStatus.New);
        await PatchAsync(created.Id, StatusBody(RequestStatus.InProgress), Quoted(created.RowVersion));

        // Stale version AND (New -> New would be invalid): the version wins, as the contract orders it.
        var response = await PatchAsync(created.Id, StatusBody(RequestStatus.New), Quoted(created.RowVersion));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_request_returns_404()
    {
        var response = await PatchAsync(987654, StatusBody(RequestStatus.InProgress), Quoted(new byte[8]));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("not-found", (await ReadJsonAsync(response)).GetProperty("errorCode").GetString());
    }

    [Theory]
    [InlineData(RequestStatus.New, RequestStatus.New)]
    [InlineData(RequestStatus.New, RequestStatus.Waiting)]
    [InlineData(RequestStatus.New, RequestStatus.Completed)]
    [InlineData(RequestStatus.InProgress, RequestStatus.New)]
    [InlineData(RequestStatus.InProgress, RequestStatus.InProgress)]
    [InlineData(RequestStatus.Waiting, RequestStatus.Waiting)]
    [InlineData(RequestStatus.Completed, RequestStatus.New)]
    [InlineData(RequestStatus.Completed, RequestStatus.InProgress)]
    [InlineData(RequestStatus.Completed, RequestStatus.Waiting)]
    [InlineData(RequestStatus.Completed, RequestStatus.Completed)]
    public async Task Invalid_transition_returns_400_and_has_no_side_effects(RequestStatus from, RequestStatus to)
    {
        var created = await Data.AddRequestAsync(status: from);

        var response = await PatchAsync(created.Id, StatusBody(to), Quoted(created.RowVersion));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ReadJsonAsync(response);
        Assert.Equal("invalid-status-transition", problem.GetProperty("errorCode").GetString());
        Assert.Equal(from.ToString(), problem.GetProperty("currentStatus").GetString());
        Assert.Equal(to.ToString(), problem.GetProperty("requestedStatus").GetString());

        var stored = await ReloadAsync(created.Id);
        Assert.Equal(from, stored.Status);
        Assert.Equal(created.RowVersion, stored.RowVersion);
        Assert.Equal(created.UpdatedAt, stored.UpdatedAt);
        Assert.Empty(await HistoryRowsAsync(created.Id));
    }

    [Fact]
    public async Task Missing_if_match_returns_428()
    {
        var created = await Data.AddRequestAsync();

        var response = await PatchAsync(created.Id, StatusBody(RequestStatus.InProgress), ifMatch: null);

        Assert.Equal((HttpStatusCode)428, response.StatusCode);
        Assert.Equal("precondition-required", (await ReadJsonAsync(response)).GetProperty("errorCode").GetString());
        Assert.Empty(await HistoryRowsAsync(created.Id));
    }

    [Theory]
    [InlineData("*")]
    [InlineData("W/\"AAAAAAAAB9E=\"")]
    [InlineData("AAAAAAAAB9E=")]
    [InlineData("\"not-base64!!!\"")]
    [InlineData("\"AAAA\"")]
    [InlineData("\"\"")]
    [InlineData("\"AAAAAAAAB9E=\", \"AAAAAAAAB9F=\"")]
    public async Task Malformed_if_match_returns_400_invalid_version(string ifMatch)
    {
        var created = await Data.AddRequestAsync();

        var response = await PatchAsync(created.Id, StatusBody(RequestStatus.InProgress), ifMatch);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid-version", (await ReadJsonAsync(response)).GetProperty("errorCode").GetString());
        Assert.Empty(await HistoryRowsAsync(created.Id));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Missing_or_blank_user_returns_400(string? user)
    {
        var created = await Data.AddRequestAsync();

        var response = await PatchAsync(created.Id, StatusBody(RequestStatus.InProgress), Quoted(created.RowVersion), user);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ReadJsonAsync(response);
        Assert.Equal("validation-failed", problem.GetProperty("errorCode").GetString());
        Assert.True(problem.GetProperty("errors").TryGetProperty("X-User-Name", out _));
        Assert.Empty(await HistoryRowsAsync(created.Id));
    }

    [Fact]
    public async Task Too_long_user_returns_400()
    {
        var created = await Data.AddRequestAsync();

        var response = await PatchAsync(
            created.Id, StatusBody(RequestStatus.InProgress), Quoted(created.RowVersion), new string('x', 101));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("{\"status\":\"Bogus\"}")]
    [InlineData("{\"status\":\"1\"}")]
    [InlineData("{\"status\":null}")]
    [InlineData("{}")]
    public async Task Invalid_status_in_body_returns_400_validation_error_even_without_if_match(string body)
    {
        var created = await Data.AddRequestAsync();

        // Input validation (step 1) precedes the missing If-Match (428).
        var response = await PatchAsync(created.Id, body, ifMatch: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ReadJsonAsync(response);
        Assert.Equal("validation-failed", problem.GetProperty("errorCode").GetString());
        Assert.True(problem.GetProperty("errors").TryGetProperty("status", out _));
    }

    [Fact]
    public async Task Missing_body_and_invalid_id_return_400()
    {
        var created = await Data.AddRequestAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await PatchAsync(created.Id, null, Quoted(created.RowVersion))).StatusCode);
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await PatchAsync(0, StatusBody(RequestStatus.InProgress), Quoted(created.RowVersion))).StatusCode);
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await PatchAsync("abc", StatusBody(RequestStatus.InProgress), Quoted(created.RowVersion))).StatusCode);
    }

    [Fact]
    public async Task Status_name_is_case_insensitive()
    {
        var created = await Data.AddRequestAsync();

        var response = await PatchAsync(created.Id, "{\"status\":\"inprogress\"}", Quoted(created.RowVersion));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task History_lists_changes_newest_first_with_all_fields()
    {
        var created = await Data.AddRequestAsync(status: RequestStatus.New);
        var etag = Quoted(created.RowVersion);
        foreach (var (status, user) in new[]
                 {
                     (RequestStatus.InProgress, "ann"), (RequestStatus.Waiting, "ben"),
                     (RequestStatus.InProgress, "cat"), (RequestStatus.Completed, "dov"),
                 })
        {
            var response = await PatchAsync(created.Id, StatusBody(status), etag, user);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            etag = response.Headers.ETag!.ToString();
        }

        var history = (await Client.GetFromJsonAsync<List<StatusHistoryItemDto>>(
            $"/api/requests/{created.Id}/history", Json.Options))!;

        Assert.Equal(["dov", "cat", "ben", "ann"], history.Select(h => h.ChangedBy));
        Assert.Equal(
            [RequestStatus.Completed, RequestStatus.InProgress, RequestStatus.Waiting, RequestStatus.InProgress],
            history.Select(h => h.NewStatus));
        Assert.Equal(
            [RequestStatus.InProgress, RequestStatus.Waiting, RequestStatus.InProgress, RequestStatus.New],
            history.Select(h => h.PreviousStatus));
        Assert.All(history, h => Assert.Equal(created.Id, h.RequestId));
        Assert.Equal(history.OrderByDescending(h => h.ChangedAt).ThenByDescending(h => h.Id), history);
    }

    [Fact]
    public async Task History_orders_equal_timestamps_by_id_descending()
    {
        var created = await Data.AddRequestAsync();
        var sameInstant = new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);
        await using (var db = Fixture.CreateDbContext())
        {
            var request = await db.Requests.SingleAsync(r => r.Id == created.Id);
            // Two audit rows with an identical ChangedAt: only the Id tiebreaker decides the order.
            db.StatusHistory.Add(((StatusChangeResult.Changed)request.ChangeStatus(RequestStatus.InProgress, "a", sameInstant)).History);
            await db.SaveChangesAsync();
            db.StatusHistory.Add(((StatusChangeResult.Changed)request.ChangeStatus(RequestStatus.Waiting, "b", sameInstant)).History);
            await db.SaveChangesAsync();
        }

        var history = (await Client.GetFromJsonAsync<List<StatusHistoryItemDto>>(
            $"/api/requests/{created.Id}/history", Json.Options))!;

        Assert.Equal(["b", "a"], history.Select(h => h.ChangedBy));
    }

    [Fact]
    public async Task History_of_a_request_without_changes_is_an_empty_array()
    {
        var created = await Data.AddRequestAsync();

        var json = await Client.GetStringAsync($"/api/requests/{created.Id}/history");

        Assert.Equal("[]", json);
    }

    [Fact]
    public async Task History_only_contains_the_requested_request()
    {
        var a = await Data.AddRequestAsync();
        var b = await Data.AddRequestAsync();
        await PatchAsync(a.Id, StatusBody(RequestStatus.InProgress), Quoted(a.RowVersion));

        var history = (await Client.GetFromJsonAsync<List<StatusHistoryItemDto>>($"/api/requests/{b.Id}/history", Json.Options))!;

        Assert.Empty(history);
    }

    [Fact]
    public async Task History_of_unknown_request_returns_404_and_invalid_id_400()
    {
        var missing = await Client.GetAsync("/api/requests/987654/history");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("not-found", (await ReadJsonAsync(missing)).GetProperty("errorCode").GetString());

        Assert.Equal(HttpStatusCode.BadRequest, (await Client.GetAsync("/api/requests/0/history")).StatusCode);
    }

    [Fact]
    public async Task Cancellation_before_the_save_persists_nothing()
    {
        var created = await Data.AddRequestAsync(status: RequestStatus.New);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            PatchWithTokenAsync(created, cts.Token));

        var stored = await ReloadAsync(created.Id);
        Assert.Equal(RequestStatus.New, stored.Status);
        Assert.Equal(created.RowVersion, stored.RowVersion);
        Assert.Empty(await HistoryRowsAsync(created.Id));
    }

    private async Task PatchWithTokenAsync(Request created, CancellationToken token)
    {
        using var message = new HttpRequestMessage(HttpMethod.Patch, $"/api/requests/{created.Id}/status")
        {
            Content = new StringContent(StatusBody(RequestStatus.InProgress), Encoding.UTF8, "application/json"),
        };
        message.Headers.TryAddWithoutValidation("If-Match", Quoted(created.RowVersion));
        message.Headers.TryAddWithoutValidation("X-User-Name", "dana");
        await Client.SendAsync(message, token);
    }
}
