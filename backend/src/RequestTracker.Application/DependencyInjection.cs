using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RequestTracker.Application.Caching;
using RequestTracker.Application.Requests;

namespace RequestTracker.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<RequestQueryService>();
        services.AddScoped<RequestSummaryService>();
        services.AddScoped<RequestStatusChanger>();
        services.AddScoped<RequestStatusService>();
        services.AddScoped<BulkStatusService>();
        services.AddSingleton<IValidateOptions<CacheOptions>, CacheOptionsValidator>();
        services.AddValidatorsFromAssemblyContaining<GetRequestQueryValidator>(ServiceLifetime.Scoped);
        return services;
    }
}
