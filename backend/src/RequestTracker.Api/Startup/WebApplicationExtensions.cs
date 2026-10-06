using Microsoft.AspNetCore.Diagnostics;
using Scalar.AspNetCore;

namespace RequestTracker.Api.Startup;

public static class WebApplicationExtensions
{
    public static WebApplication UseApi(this WebApplication app)
    {
        app.UseExceptionHandler(new ExceptionHandlerOptions
        {
            // The handler logs the exception itself; skip the middleware's duplicate (and client aborts).
            SuppressDiagnosticsCallback = _ => true,
        });
        app.UseStatusCodePages();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.MapScalarApiReference();
        }

        app.MapControllers();
        app.MapHealthChecks("/health", new() { ResponseWriter = HealthResponseWriter.WriteAsync });
        return app;
    }

    public static void LogStartupSummary(this WebApplication app)
    {
        // Configuration summary only: never the connection string itself.
        app.Logger.LogInformation(
            "Starting Request Tracker API. Environment={Environment}, ConnectionStringConfigured={ConnectionStringConfigured}, OpenApiUi={OpenApiUi}",
            app.Environment.EnvironmentName,
            !string.IsNullOrWhiteSpace(app.Configuration.GetConnectionString("Default")),
            app.Environment.IsDevelopment());
    }
}
