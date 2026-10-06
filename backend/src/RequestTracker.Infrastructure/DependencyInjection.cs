using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RequestTracker.Application.Abstractions;
using RequestTracker.Application.Caching;
using RequestTracker.Infrastructure.Caching;
using RequestTracker.Infrastructure.Persistence;

namespace RequestTracker.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "Default";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // The connection string is read when the DbContext is first resolved, not while composing, so test hosts
        // and late configuration sources can supply it. AddDbContext registers AppDbContext as scoped.
        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionStringName)
                ?? throw new InvalidOperationException(
                    $"Connection string 'ConnectionStrings:{ConnectionStringName}' is not configured.");
            options.UseSqlServer(connectionString);
        });
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database");

        // Cache: HybridCache (singleton, in-process L1) behind a scoped wrapper. TTLs come from configuration and are
        // validated at startup. Adding a distributed L2 later is a registration change here, not a code change.
        services.AddOptions<CacheOptions>().BindConfiguration(CacheOptions.SectionName).ValidateOnStart();
        services.AddHybridCache();
        services.AddScoped<IRequestReadCache, RequestReadCache>();
        return services;
    }
}
