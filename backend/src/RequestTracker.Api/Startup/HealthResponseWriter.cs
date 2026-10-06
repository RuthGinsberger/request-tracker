using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace RequestTracker.Api.Startup;

/// <summary>Health response with per-check status only; exception text never leaves the server.</summary>
public static class HealthResponseWriter
{
    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        var body = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new { name = e.Key, status = e.Value.Status.ToString() }),
        };
        return context.Response.WriteAsync(JsonSerializer.Serialize(body, JsonSerializerOptions.Web));
    }
}
