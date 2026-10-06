using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RequestTracker.Application;
using RequestTracker.Application.Requests;
using RequestTracker.Domain;
using RequestTracker.IntegrationTests.Infrastructure;

namespace RequestTracker.IntegrationTests.Requests;

public sealed class GetRequestByIdTests(SqlServerFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task Existing_request_returns_200_with_dto_and_etag_equal_to_quoted_version()
    {
        var created = await Data.AddRequestAsync(
            title: "VPN access",
            organizationName: "Globex",
            priority: RequestPriority.High,
            assignedTo: "noa",
            status: RequestStatus.InProgress,
            createdAt: new DateTime(2026, 2, 1, 8, 0, 0, DateTimeKind.Utc),
            updatedAt: new DateTime(2026, 2, 2, 10, 15, 0, DateTimeKind.Utc));

        var response = await Client.GetAsync($"/api/requests/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<RequestDetailsDto>(
            Json.Options);
        Assert.NotNull(dto);
        Assert.Equal(created.Id, dto.Id);
        Assert.Equal("VPN access", dto.Title);
        Assert.Equal("Globex", dto.OrganizationName);
        Assert.Equal(RequestStatus.InProgress, dto.Status);
        Assert.Equal(RequestPriority.High, dto.Priority);
        Assert.Equal("noa", dto.AssignedTo);
        Assert.Equal(new DateTime(2026, 2, 1, 8, 0, 0, DateTimeKind.Utc), dto.CreatedAt);
        Assert.Equal(new DateTime(2026, 2, 2, 10, 15, 0, DateTimeKind.Utc), dto.UpdatedAt);

        // The version is the base64 rowversion the database assigned, and the ETag is exactly that, quoted.
        Assert.Equal(VersionToken.Encode(created.RowVersion), dto.Version);
        Assert.Equal($"\"{dto.Version}\"", response.Headers.ETag?.ToString());
    }

    [Fact]
    public async Task Response_body_uses_camelCase_and_string_enums_and_no_entity_fields()
    {
        var created = await Data.AddRequestAsync(status: RequestStatus.Waiting, assignedTo: null);

        var json = await Client.GetStringAsync($"/api/requests/{created.Id}");

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal("Waiting", root.GetProperty("status").GetString());
        Assert.Equal("Medium", root.GetProperty("priority").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("assignedTo").ValueKind);
        Assert.Equal(
            ["assignedTo", "createdAt", "id", "organizationName", "priority", "status", "title", "updatedAt", "version"],
            root.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Missing_request_returns_404_problem_details()
    {
        var response = await Client.GetAsync("/api/requests/987654");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await ReadProblemAsync(response);
        Assert.Equal(404, problem.GetProperty("status").GetInt32());
        Assert.Equal("not-found", problem.GetProperty("errorCode").GetString());
        Assert.Equal("urn:requesttracker:error:not-found", problem.GetProperty("type").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("traceId").GetString()));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-5")]
    public async Task Invalid_id_returns_400_validation_problem_details(string id)
    {
        var response = await Client.GetAsync($"/api/requests/{id}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await ReadProblemAsync(response);
        Assert.Equal("validation-failed", problem.GetProperty("errorCode").GetString());
        Assert.Equal("urn:requesttracker:error:validation-failed", problem.GetProperty("type").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("traceId").GetString()));
        Assert.True(problem.GetProperty("errors").TryGetProperty("id", out var idErrors));
        Assert.NotEmpty(idErrors.EnumerateArray());
    }

    [Fact]
    public async Task Unknown_route_returns_problem_details_too()
    {
        var response = await Client.GetAsync("/api/nothing-here");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await ReadProblemAsync(response);
        Assert.Equal("not-found", problem.GetProperty("errorCode").GetString());
    }

    private static async Task<JsonElement> ReadProblemAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();
}
