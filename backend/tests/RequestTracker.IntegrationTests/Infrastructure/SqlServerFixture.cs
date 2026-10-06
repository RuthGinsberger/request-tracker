using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Respawn;
using Respawn.Graph;
using RequestTracker.Infrastructure.Caching;
using RequestTracker.Infrastructure.Persistence;

namespace RequestTracker.IntegrationTests.Infrastructure;

/// <summary>
/// One real SQL Server database per test run: created and migrated once, wiped between tests, dropped at the end.
/// The server comes from <c>INTEGRATIONTESTS_SQLSERVER</c> (a connection string, or just a server name that is used
/// with Windows authentication); it defaults to LocalDB. No Docker or Testcontainers needed (D-010).
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    public const string ServerVariable = "INTEGRATIONTESTS_SQLSERVER";
    private const string DefaultServer = "(localdb)\\MSSQLLocalDB";

    private readonly string databaseName = $"RequestTracker_Tests_{Guid.NewGuid():N}";
    private Respawner? respawner;

    public string ConnectionString { get; private set; } = string.Empty;

    /// <summary>The API in the Development environment (the OpenAPI UI is mapped there).</summary>
    public ApiFactory Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable(ServerVariable);
        var builder = new SqlConnectionStringBuilder(BuildServerConnectionString(configured))
        {
            InitialCatalog = databaseName,
        };
        ConnectionString = builder.ConnectionString;

        await using (var db = CreateDbContext())
        {
            await db.Database.MigrateAsync(); // creates the database, then applies the real migrations
        }

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.SqlServer,
            TablesToIgnore = [new Table("__EFMigrationsHistory")],
        });

        Factory = CreateFactory(ConnectionString, "Development");
    }

    public async Task DisposeAsync()
    {
        if (Factory is not null)
        {
            await Factory.DisposeAsync();
        }

        await using var db = CreateDbContext();
        await db.Database.EnsureDeletedAsync(); // clears connection pools first, then drops the database
    }

    public static ApiFactory CreateFactory(string connectionString, string environment) => new(connectionString, environment);

    /// <summary>Deletes all rows (keeps the schema) so every test starts from an empty database.</summary>
    public async Task ResetAsync()
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await respawner!.ResetAsync(connection);

        // The API (and its in-process cache) lives for the whole run, so cached values must not leak between tests.
        var cache = Factory.Services.GetRequiredService<HybridCache>();
        await cache.RemoveByTagAsync(RequestReadCache.SummaryTag);
        await cache.RemoveByTagAsync(RequestReadCache.LookupsTag);
    }

    public AppDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(ConnectionString).Options);

    /// <summary>A connection string to a database that does not exist on the same server (simulates "DB down").</summary>
    public string MissingDatabaseConnectionString() =>
        new SqlConnectionStringBuilder(ConnectionString)
        {
            InitialCatalog = $"RequestTracker_Missing_{Guid.NewGuid():N}",
            ConnectTimeout = 5,
        }.ConnectionString;

    private static string BuildServerConnectionString(string? configured)
    {
        var value = string.IsNullOrWhiteSpace(configured) ? DefaultServer : configured;
        return value.Contains('=')
            ? value
            : $"Server={value};Trusted_Connection=True;TrustServerCertificate=True";
    }
}
