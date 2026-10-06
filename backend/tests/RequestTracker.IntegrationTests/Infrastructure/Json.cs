using System.Text.Json;
using System.Text.Json.Serialization;

namespace RequestTracker.IntegrationTests.Infrastructure;

/// <summary>Client-side view of the API's JSON contract.</summary>
public static class Json
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };
}
