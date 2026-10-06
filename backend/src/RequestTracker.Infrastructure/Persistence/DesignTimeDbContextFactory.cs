using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RequestTracker.Infrastructure.Persistence;

/// <summary>Used by `dotnet ef` and the seeder. The connection string comes from ConnectionStrings__Default, else LocalDB.</summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public const string ConnectionStringVariable = "ConnectionStrings__Default";

    private const string LocalDbConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=RequestTracker;Trusted_Connection=True;TrustServerCertificate=True";

    public static string ResolveConnectionString() =>
        Environment.GetEnvironmentVariable(ConnectionStringVariable) ?? LocalDbConnectionString;

    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(ResolveConnectionString()).Options;
        return new AppDbContext(options);
    }
}
