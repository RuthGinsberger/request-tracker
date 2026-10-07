using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RequestTracker.Api.Errors;
using RequestTracker.Api.Http;
using RequestTracker.Application;
using RequestTracker.Application.Abstractions;
using RequestTracker.Infrastructure;

namespace RequestTracker.Api.Startup;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApi(this IServiceCollection services)
    {
        services.AddApplication();
        services.AddInfrastructure();

        services.AddControllers(options => options.Filters.Add<ValidationFilter>())
            .AddJsonOptions(options => ApiJson.Configure(options.JsonSerializerOptions))
            .ConfigureApiBehaviorOptions(options =>
                // Model-binding errors use the same ValidationProblemDetails as FluentValidation failures.
                options.InvalidModelStateResponseFactory = context =>
                    new ProblemResult(ApiProblems.Validation(context.HttpContext, context.ModelState)));
        services.ConfigureHttpJsonOptions(options => ApiJson.Configure(options.SerializerOptions));

        services.AddProblemDetails(options =>
            options.CustomizeProblemDetails = context =>
                ApiProblems.Normalize(context.HttpContext, context.ProblemDetails));
        services.AddExceptionHandler<GlobalExceptionHandler>();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HeaderCurrentUser>();
        services.TryAddSingleton(TimeProvider.System);

        services.AddOpenApi(options => options.AddOperationTransformer<DocumentedHeaderTransformer>());
        return services;
    }
}
