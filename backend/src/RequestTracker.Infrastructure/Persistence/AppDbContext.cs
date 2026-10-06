using Microsoft.EntityFrameworkCore;
using RequestTracker.Application.Abstractions;
using RequestTracker.Domain;

namespace RequestTracker.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<Request> Requests => Set<Request>();

    public DbSet<RequestStatusHistory> StatusHistory => Set<RequestStatusHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        // All timestamps are UTC: datetime2 in the DB, Kind=Utc when read back.
        configurationBuilder.Properties<DateTime>()
            .HaveColumnType("datetime2")
            .HaveConversion<UtcDateTimeConverter>();
}
