using RequestTracker.Domain;

namespace RequestTracker.IntegrationTests.Infrastructure;

/// <summary>Inserts specific rows (never the 100k seed). Defaults make a valid request; override what a test cares about.</summary>
public sealed class TestData(SqlServerFixture fixture)
{
    public async Task<Request> AddRequestAsync(
        string title = "Printer is broken",
        string organizationName = "Acme Ltd",
        RequestPriority priority = RequestPriority.Medium,
        string? assignedTo = "dana",
        RequestStatus status = RequestStatus.New,
        DateTime? createdAt = null,
        DateTime? updatedAt = null)
    {
        var created = createdAt ?? new DateTime(2026, 1, 15, 9, 30, 0, DateTimeKind.Utc);
        var request = Request.Create(title, organizationName, priority, assignedTo, created, status, updatedAt);

        await using var db = fixture.CreateDbContext();
        db.Requests.Add(request);
        await db.SaveChangesAsync();
        return request; // Id and RowVersion are filled in by the database
    }
}
