using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using RequestTracker.Application;
using RequestTracker.Application.Requests;
using RequestTracker.Domain;
using RequestTracker.IntegrationTests.Infrastructure;

namespace RequestTracker.IntegrationTests.Requests;

public sealed class ListRequestsTests(SqlServerFixture fixture) : IntegrationTestBase(fixture)
{
    private static DateTime Utc(int month, int day, int hour = 0, int minute = 0, int second = 0, int millisecond = 0) =>
        new(2026, month, day, hour, minute, second, millisecond, DateTimeKind.Utc);

    /// <summary>
    /// Five rows, inserted A..E so Ids ascend in that order.
    /// Priority: A High, B Low, C Medium, D High, E Low. Status: A New, B InProgress, C Waiting, D Completed, E New.
    /// UpdatedAt ties on purpose: A and B share a timestamp.
    /// </summary>
    private async Task<Dictionary<string, Request>> SeedAsync()
    {
        var rows = new Dictionary<string, Request>
        {
            ["A"] = await Data.AddRequestAsync("Alpha printer", "Acme", RequestPriority.High, "dana", RequestStatus.New,
                Utc(1, 10, 8), Utc(1, 20)),
            ["B"] = await Data.AddRequestAsync("Beta VPN", "Globex", RequestPriority.Low, "noa", RequestStatus.InProgress,
                Utc(1, 11), Utc(1, 20)),
            ["C"] = await Data.AddRequestAsync("Gamma 100% done", "Acme", RequestPriority.Medium, "dana", RequestStatus.Waiting,
                Utc(1, 11, 23, 59, 59, 999), Utc(1, 15)),
            ["D"] = await Data.AddRequestAsync("delta_x", "Initech", RequestPriority.High, null, RequestStatus.Completed,
                Utc(1, 12), Utc(1, 25)),
            ["E"] = await Data.AddRequestAsync("Echo note", "Umbrella", RequestPriority.Low, "noa", RequestStatus.New,
                Utc(1, 13), Utc(1, 10)),
        };
        return rows;
    }

    private async Task<PagedResult<RequestListItemDto>> GetPageAsync(string query = "")
    {
        var response = await Client.GetAsync($"/api/requests{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PagedResult<RequestListItemDto>>(Json.Options);
        return Assert.IsType<PagedResult<RequestListItemDto>>(page);
    }

    private async Task<int[]> GetIdsAsync(string query) => [.. (await GetPageAsync(query)).Items.Select(i => i.Id)];

    private static int[] Ids(Dictionary<string, Request> rows, string names) =>
        [.. names.Select(n => rows[n.ToString()].Id)];

    // ---------- defaults, shape, paging ----------

    [Fact]
    public async Task Default_query_returns_first_page_sorted_by_createdAt_descending()
    {
        var rows = await SeedAsync();

        var page = await GetPageAsync();

        Assert.Equal(Ids(rows, "EDCBA"), page.Items.Select(i => i.Id));
        Assert.Equal(1, page.Page);
        Assert.Equal(20, page.PageSize);
        Assert.Equal(5, page.TotalCount);
        Assert.Equal(1, page.TotalPages);
    }

    [Fact]
    public async Task Items_expose_only_dto_fields_with_string_enums_and_version()
    {
        var rows = await SeedAsync();

        var json = await Client.GetStringAsync("/api/requests?search=delta");

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal(
            ["items", "page", "pageSize", "totalCount", "totalPages"],
            root.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        var item = root.GetProperty("items")[0];
        Assert.Equal(
            ["assignedTo", "createdAt", "id", "organizationName", "priority", "status", "title", "updatedAt", "version"],
            item.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        Assert.Equal("Completed", item.GetProperty("status").GetString());
        Assert.Equal("High", item.GetProperty("priority").GetString());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("assignedTo").ValueKind);
        Assert.Equal(VersionToken.Encode(rows["D"].RowVersion), item.GetProperty("version").GetString());
    }

    [Fact]
    public async Task Paging_returns_metadata_and_pages_concatenate_to_the_full_ordered_list()
    {
        var rows = await SeedAsync();

        var first = await GetPageAsync("?pageSize=2&page=1&sortBy=priority&sortDirection=asc");
        var second = await GetPageAsync("?pageSize=2&page=2&sortBy=priority&sortDirection=asc");
        var third = await GetPageAsync("?pageSize=2&page=3&sortBy=priority&sortDirection=asc");

        Assert.All([first, second, third], p =>
        {
            Assert.Equal(5, p.TotalCount);
            Assert.Equal(3, p.TotalPages);
            Assert.Equal(2, p.PageSize);
        });
        Assert.Equal([2, 2, 1], new[] { first.Items.Count, second.Items.Count, third.Items.Count });
        // Low (B, E), Medium (C), High (A, D); ties by Id ascending.
        Assert.Equal(
            Ids(rows, "BECAD"),
            first.Items.Concat(second.Items).Concat(third.Items).Select(i => i.Id));
    }

    [Fact]
    public async Task Page_past_the_end_returns_200_with_empty_items_and_true_totals()
    {
        await SeedAsync();

        var page = await GetPageAsync("?pageSize=2&page=4");

        Assert.Empty(page.Items);
        Assert.Equal(4, page.Page);
        Assert.Equal(5, page.TotalCount);
        Assert.Equal(3, page.TotalPages);
    }

    [Fact]
    public async Task No_matches_gives_empty_items_and_zero_total_pages()
    {
        await SeedAsync();

        var page = await GetPageAsync("?organizationName=Nobody");

        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
        Assert.Equal(0, page.TotalPages);
    }

    // ---------- filters ----------

    [Theory]
    [InlineData("?status=New", "EA")]
    [InlineData("?status=New&status=Waiting", "ECA")]
    [InlineData("?status=inprogress", "B")]
    [InlineData("?priority=High", "DA")]
    [InlineData("?priority=Low&priority=Medium", "ECB")]
    [InlineData("?organizationName=Acme", "CA")]
    [InlineData("?organizationName=acme", "CA")]
    [InlineData("?assignedTo=noa", "EB")]
    public async Task Each_filter_alone(string query, string expected)
    {
        var rows = await SeedAsync();

        Assert.Equal(Ids(rows, expected), await GetIdsAsync(query));
    }

    [Theory]
    [InlineData("?createdFrom=2026-01-11", "EDCB")]
    [InlineData("?createdTo=2026-01-11", "CBA")]
    [InlineData("?createdFrom=2026-01-11&createdTo=2026-01-11", "CB")] // whole day, 00:00:00 through 23:59:59.999
    [InlineData("?createdFrom=2026-01-12&createdTo=2026-01-12", "D")]
    [InlineData("?createdFrom=2026-02-01", "")]
    public async Task Date_range_covers_whole_days(string query, string expected)
    {
        var rows = await SeedAsync();

        Assert.Equal(Ids(rows, expected), await GetIdsAsync(query));
    }

    [Fact]
    public async Task Filters_combine_with_and()
    {
        var rows = await SeedAsync();

        var ids = await GetIdsAsync(
            "?status=New&status=Waiting&organizationName=Acme&assignedTo=dana&priority=High&priority=Medium"
            + "&createdFrom=2026-01-10&createdTo=2026-01-11&search=a");

        // Acme + dana + New/Waiting + High/Medium + Jan 10-11, title or org starting with "a": A (Alpha, Acme) and C (Acme).
        Assert.Equal(Ids(rows, "CA"), ids);
    }

    [Fact]
    public async Task Repeated_filter_values_are_deduplicated_and_case_insensitive()
    {
        var rows = await SeedAsync();

        Assert.Equal(Ids(rows, "EA"), await GetIdsAsync("?status=New&status=NEW&status=new"));
    }

    // ---------- search ----------

    [Theory]
    [InlineData("?search=alpha", "A")] // Title prefix, case-insensitive
    [InlineData("?search=Beta V", "B")]
    [InlineData("?search=umb", "E")] // OrganizationName prefix
    [InlineData("?search=Acme", "CA")]
    [InlineData("?search=printer", "")] // not a prefix: no infix matching
    [InlineData("?search=lpha", "")]
    [InlineData("?search=%20%20alpha%20%20", "A")] // trimmed
    public async Task Search_matches_title_or_organization_prefix(string query, string expected)
    {
        var rows = await SeedAsync();

        Assert.Equal(Ids(rows, expected), await GetIdsAsync(query));
    }

    [Theory]
    [InlineData("?search=")]
    [InlineData("?search=%20%20%20")]
    public async Task Blank_search_is_ignored(string query)
    {
        var rows = await SeedAsync();

        Assert.Equal(Ids(rows, "EDCBA"), await GetIdsAsync(query));
    }

    [Theory]
    [InlineData("100%25", "pct")] // "%" is literal: "100% sure" matches, "1000 sure" does not
    [InlineData("delta_", "under")] // "_" is literal: "delta_x" matches, "deltaXray" does not
    [InlineData("%5Bb", "bracket")] // "[" is literal: "[b-tag]" matches, "b-thing" does not
    [InlineData("%25", "")] // a lone "%" must not match everything
    [InlineData("_", "")]
    [InlineData("%5C", "slash")] // backslash is literal
    public async Task Wildcard_characters_in_search_are_matched_literally(string search, string expectedKey)
    {
        var rows = new Dictionary<string, Request>
        {
            ["pct"] = await Data.AddRequestAsync("100% sure"),
            ["pct-decoy"] = await Data.AddRequestAsync("1000 sure"),
            ["under"] = await Data.AddRequestAsync("delta_x"),
            ["under-decoy"] = await Data.AddRequestAsync("deltaXray"),
            ["bracket"] = await Data.AddRequestAsync("[b-tag] note"),
            ["bracket-decoy"] = await Data.AddRequestAsync("b-thing"),
            ["slash"] = await Data.AddRequestAsync("\\server share"),
        };

        var ids = await GetIdsAsync($"?search={search}&sortBy=title");

        Assert.Equal(expectedKey.Length == 0 ? Array.Empty<int>() : new[] { rows[expectedKey].Id }, ids);
    }

    // ---------- sorting ----------

    [Theory]
    [InlineData("createdAt", "asc", "ABCDE")]
    [InlineData("createdAt", "desc", "EDCBA")]
    // A and B share UpdatedAt; the Id tie-breaker follows the direction.
    [InlineData("updatedAt", "asc", "ECABD")]
    [InlineData("updatedAt", "desc", "DBACE")]
    // Priority is numeric (Low < Medium < High): Low = B, E; Medium = C; High = A, D.
    [InlineData("priority", "asc", "BECAD")]
    [InlineData("priority", "desc", "DACEB")]
    // Status is numeric: New = A, E; InProgress = B; Waiting = C; Completed = D.
    [InlineData("status", "asc", "AEBCD")]
    [InlineData("status", "desc", "DCBEA")]
    [InlineData("title", "asc", "ABDEC")] // Alpha, Beta, delta_x, Echo, Gamma (case-insensitive collation)
    [InlineData("title", "desc", "CEDBA")]
    public async Task Each_sort_field_ascending_and_descending_with_stable_tie_breaker(
        string sortBy, string direction, string expected)
    {
        var rows = await SeedAsync();

        Assert.Equal(Ids(rows, expected), await GetIdsAsync($"?sortBy={sortBy}&sortDirection={direction}"));
    }

    [Fact]
    public async Task Sort_names_are_case_insensitive_and_direction_defaults_to_descending()
    {
        var rows = await SeedAsync();

        Assert.Equal(Ids(rows, "CEDBA"), await GetIdsAsync("?sortBy=TITLE&sortDirection=DESC"));
        Assert.Equal(Ids(rows, "CEDBA"), await GetIdsAsync("?sortBy=title"));
        Assert.Equal(Ids(rows, "ABCDE"), await GetIdsAsync("?sortDirection=ASC"));
    }

    // ---------- validation ----------

    [Theory]
    [InlineData("page=0", "page")]
    [InlineData("page=-1", "page")]
    [InlineData("page=1000001", "page")]
    [InlineData("page=abc", "page")]
    [InlineData("pageSize=0", "pageSize")]
    [InlineData("pageSize=101", "pageSize")]
    [InlineData("pageSize=-5", "pageSize")]
    [InlineData("pageSize=many", "pageSize")]
    [InlineData("status=Bogus", "status")]
    [InlineData("status=New&status=Bogus", "status")]
    [InlineData("status=1", "status")]
    [InlineData("priority=Urgent", "priority")]
    [InlineData("createdFrom=not-a-date", "createdFrom")]
    [InlineData("createdTo=2026-13-45", "createdTo")]
    [InlineData("createdFrom=2026-02-01&createdTo=2026-01-31", "createdTo")]
    [InlineData("sortBy=bogus", "sortBy")]
    [InlineData("sortBy=id", "sortBy")]
    [InlineData("sortBy=0", "sortBy")]
    [InlineData("sortDirection=up", "sortDirection")]
    public async Task Invalid_parameter_returns_400_validation_problem_naming_the_field(string query, string field)
    {
        var response = await Client.GetAsync($"/api/requests?{query}");

        await AssertValidationProblemAsync(response, field);
    }

    [Fact]
    public async Task Too_long_values_return_400_naming_the_field()
    {
        await AssertValidationProblemAsync(await Client.GetAsync($"/api/requests?search={new string('a', 101)}"), "search");
        await AssertValidationProblemAsync(
            await Client.GetAsync($"/api/requests?organizationName={new string('o', 201)}"), "organizationName");
        await AssertValidationProblemAsync(await Client.GetAsync($"/api/requests?assignedTo={new string('n', 101)}"), "assignedTo");
    }

    [Fact]
    public async Task Search_length_limit_applies_after_trimming_and_100_is_accepted()
    {
        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync($"/api/requests?search={new string('a', 100)}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await Client.GetAsync($"/api/requests?search=%20%20{new string('a', 100)}%20%20")).StatusCode);
    }

    [Fact]
    public async Task Boundary_values_are_accepted()
    {
        await SeedAsync();

        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync("/api/requests?pageSize=1&page=1000000")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync("/api/requests?pageSize=100")).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await Client.GetAsync("/api/requests?createdFrom=2026-01-11&createdTo=2026-01-11")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync("/api/requests?createdTo=9999-12-31")).StatusCode);
    }

    [Fact]
    public async Task Several_invalid_parameters_are_all_reported()
    {
        var response = await Client.GetAsync("/api/requests?page=0&pageSize=500&sortBy=nope");

        var problem = await AssertValidationProblemAsync(response, "page");
        var errors = problem.GetProperty("errors");
        Assert.True(errors.TryGetProperty("pageSize", out _));
        Assert.True(errors.TryGetProperty("sortBy", out _));
    }

    [Fact]
    public async Task Unknown_status_message_lists_the_allowed_values()
    {
        var response = await Client.GetAsync("/api/requests?status=Bogus");

        var problem = await AssertValidationProblemAsync(response, "status");
        var message = problem.GetProperty("errors").GetProperty("status")[0].GetString();
        Assert.Contains("InProgress", message);
    }

    // ---------- cancellation ----------

    [Fact]
    public async Task Cancelled_token_stops_the_query()
    {
        await SeedAsync();
        await using var db = Fixture.CreateDbContext();
        var service = new RequestQueryService(db, NullLogger<RequestQueryService>.Instance);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ListAsync(new RequestListQuery(), cts.Token));
    }

    private static async Task<JsonElement> AssertValidationProblemAsync(HttpResponseMessage response, string field)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("validation-failed", problem.GetProperty("errorCode").GetString());
        Assert.Equal(400, problem.GetProperty("status").GetInt32());
        Assert.True(
            problem.GetProperty("errors").TryGetProperty(field, out var messages),
            $"errors should name '{field}' but were: {problem.GetProperty("errors")}");
        Assert.NotEmpty(messages.EnumerateArray());
        return problem;
    }
}
