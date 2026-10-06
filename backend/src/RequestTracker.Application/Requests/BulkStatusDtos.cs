using System.Text.Json;
using System.Text.Json.Serialization;

namespace RequestTracker.Application.Requests;

/// <summary>One item of a bulk update: the request id and the version (plain base64, no quotes) the client last saw.</summary>
public sealed record BulkStatusItem(int Id, string? Version);

/// <summary>
/// Body of <c>POST /api/requests/bulk-status</c>. All items move to <see cref="TargetStatus"/>. A string, like the single
/// update, so a bad value gives one clear error (D-039).
/// </summary>
public sealed record BulkStatusBody(string? TargetStatus, IReadOnlyList<BulkStatusItem>? Items);

/// <summary>Per-item result of a bulk update, serialized as (property-level converter, which beats the global PascalCase one) <c>updated | notFound | conflict | invalidTransition</c>.</summary>
public enum BulkItemOutcome
{
    Updated,
    NotFound,
    Conflict,
    InvalidTransition,
}

public sealed class BulkItemOutcomeJsonConverter : JsonStringEnumConverter<BulkItemOutcome>
{
    public BulkItemOutcomeJsonConverter()
        : base(JsonNamingPolicy.CamelCase, allowIntegerValues: false)
    {
    }
}

/// <param name="NewVersion">Only for <see cref="BulkItemOutcome.Updated"/>.</param>
/// <param name="Message">Only for failures.</param>
public sealed record BulkItemResultDto(
    int Id,
    [property: JsonConverter(typeof(BulkItemOutcomeJsonConverter))] BulkItemOutcome Outcome,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? NewVersion,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Message)
{
    [JsonIgnore]
    public bool IsUpdated => Outcome == BulkItemOutcome.Updated;
}

public sealed record BulkStatusSummaryDto(int Requested, int Updated, int NotFound, int Conflict, int InvalidTransition);

public sealed record BulkStatusResultDto(BulkStatusSummaryDto Summary, IReadOnlyList<BulkItemResultDto> Results);
