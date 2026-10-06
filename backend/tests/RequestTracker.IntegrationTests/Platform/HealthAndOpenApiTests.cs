using System.Net;
using System.Text.Json;
using RequestTracker.IntegrationTests.Infrastructure;

namespace RequestTracker.IntegrationTests.Platform;

public sealed class HealthAndOpenApiTests(SqlServerFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task Health_reports_healthy_database()
    {
        var response = await Client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Healthy", body.RootElement.GetProperty("status").GetString());
        var check = Assert.Single(body.RootElement.GetProperty("checks").EnumerateArray());
        Assert.Equal("database", check.GetProperty("name").GetString());
        Assert.Equal("Healthy", check.GetProperty("status").GetString());
    }

    [Fact]
    public async Task OpenApi_document_and_ui_are_available_in_development()
    {
        var document = await Client.GetAsync("/openapi/v1.json");
        var ui = await Client.GetAsync("/scalar/v1");

        Assert.Equal(HttpStatusCode.OK, document.StatusCode);
        Assert.Contains("/api/requests/{id}", await document.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, ui.StatusCode);
    }
}

/// <summary>Unreachable database: health turns unhealthy and API calls fail safely without leaking details.</summary>
public sealed class DatabaseDownTests(SqlServerFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task Health_is_unhealthy_and_api_calls_return_generic_500_problem_details()
    {
        var missingDatabase = Fixture.MissingDatabaseConnectionString();
        await using var factory = SqlServerFixture.CreateFactory(missingDatabase, "Production");
        using var client = factory.CreateClient();

        var health = await client.GetAsync("/health");
        var healthBody = await health.Content.ReadAsStringAsync();
        var call = await client.GetAsync("/api/requests/1");
        var callBody = await call.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, health.StatusCode);
        Assert.Contains("Unhealthy", healthBody);

        Assert.Equal(HttpStatusCode.InternalServerError, call.StatusCode);
        Assert.Equal("application/problem+json", call.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(callBody);
        Assert.Equal("internal-error", problem.RootElement.GetProperty("errorCode").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.RootElement.GetProperty("traceId").GetString()));
        Assert.False(problem.RootElement.TryGetProperty("stackTrace", out _));

        // Nothing about the database, server, or exception may reach the client.
        foreach (var body in new[] { healthBody, callBody })
        {
            Assert.DoesNotContain("RequestTracker_Missing", body);
            Assert.DoesNotContain("SqlException", body);
            Assert.DoesNotContain("Cannot open database", body);
            Assert.DoesNotContain("localdb", body, StringComparison.OrdinalIgnoreCase);
        }
    }
}
