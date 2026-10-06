using System.Text.Json;
using System.Text.Json.Serialization;

namespace RequestTracker.Api.Http;

/// <summary>JSON contract: camelCase properties, enums as their names ("InProgress"), numbers never accepted for enums.</summary>
public static class ApiJson
{
    public static void Configure(JsonSerializerOptions options)
    {
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.Converters.Add(new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false));
    }
}
