using System.Diagnostics;
using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using RequestTracker.Infrastructure.Persistence;

namespace RequestTracker.Seeder;

public static class SeederApp
{
    public const int Success = 0;
    public const int Failure = 1;
    public const int UsageError = 2;
    public const int Cancelled = 130;

    private const int ConnectAttempts = 6;

    public static async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var parsed = SeederOptions.Parse(args, TimeProvider.System);
        if (parsed.ShowHelp)
        {
            await output.WriteLineAsync(SeederOptions.Usage);
            return Success;
        }

        if (parsed.Options is not { } options)
        {
            await error.WriteLineAsync($"Error: {parsed.Error}");
            await error.WriteLineAsync(SeederOptions.Usage);
            return UsageError;
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            return await SeedAsync(options, output, stopwatch, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            await error.WriteLineAsync("Cancelled; the seed transaction was rolled back.");
            return Cancelled;
        }
        catch (Exception ex) when (ex is SqlException or InvalidOperationException or DbUpdateException)
        {
            // Only the message is printed; it does not contain the connection string.
            await error.WriteLineAsync($"Seeding failed: {ex.Message}");
            return Failure;
        }
    }

    private static async Task<int> SeedAsync(
        SeederOptions options, TextWriter output, Stopwatch stopwatch, CancellationToken cancellationToken)
    {
        var connectionString = DesignTimeDbContextFactory.ResolveConnectionString();
        await WaitForServerAsync(connectionString, output, cancellationToken);

        var dbOptions = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connectionString).Options;
        await using var db = new AppDbContext(dbOptions);

        if (options.Reset)
        {
            await output.WriteLineAsync("Dropping the database...");
            await db.Database.EnsureDeletedAsync(cancellationToken);
        }

        await output.WriteLineAsync("Applying migrations...");
        await db.Database.MigrateAsync(cancellationToken);

        if (!options.Reset && await db.Requests.AnyAsync(cancellationToken))
        {
            var existing = await db.Requests.CountAsync(cancellationToken);
            if (options.IfEmpty)
            {
                await output.WriteLineAsync(
                    string.Create(CultureInfo.InvariantCulture, $"Requests already holds {existing:N0} rows; nothing to do (--if-empty)."));
                return Success;
            }

            throw new InvalidOperationException(string.Create(
                CultureInfo.InvariantCulture,
                $"Requests already holds {existing:N0} rows. Use --reset to recreate the data, or --if-empty to skip seeding."));
        }

        await output.WriteLineAsync(string.Create(
            CultureInfo.InvariantCulture,
            $"Seeding {options.Count:N0} requests (seed {options.Seed}, anchor {options.AnchorUtc:yyyy-MM-dd})..."));

        var generator = new RequestGenerator(options.Seed, options.AnchorUtc);
        var written = await RequestBulkWriter.WriteAsync(
            connectionString,
            generator.Generate(options.Count),
            total => output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"  {total:N0} / {options.Count:N0}")),
            cancellationToken);

        var rows = await db.Requests.CountAsync(cancellationToken);
        stopwatch.Stop();
        await output.WriteLineAsync(string.Create(
            CultureInfo.InvariantCulture,
            $"Done: {written:N0} rows written, {rows:N0} in Requests, elapsed {stopwatch.Elapsed.TotalSeconds:F1} s."));
        return Success;
    }

    /// <summary>Waits for the SQL Server instance itself (the database may not exist yet) with exponential backoff.</summary>
    private static async Task WaitForServerAsync(string connectionString, TextWriter output, CancellationToken cancellationToken)
    {
        var master = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "master", ConnectTimeout = 5 };
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var connection = new SqlConnection(master.ConnectionString);
                await connection.OpenAsync(cancellationToken);
                return;
            }
            catch (SqlException ex)
            {
                if (attempt == ConnectAttempts)
                {
                    throw new InvalidOperationException(
                        $"SQL Server is unreachable after {ConnectAttempts} attempts: {ex.Message}", ex);
                }

                var delay = TimeSpan.FromSeconds(Math.Min(Math.Pow(2, attempt), 15));
                await output.WriteLineAsync(string.Create(
                    CultureInfo.InvariantCulture,
                    $"SQL Server not ready (attempt {attempt}/{ConnectAttempts}); retrying in {delay.TotalSeconds:F0} s..."));
                await Task.Delay(delay, cancellationToken);
            }
        }
    }
}
