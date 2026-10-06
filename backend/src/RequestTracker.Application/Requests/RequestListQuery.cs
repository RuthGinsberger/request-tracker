namespace RequestTracker.Application.Requests;

/// <summary>
/// Raw query-string parameters of <c>GET /api/requests</c>. Everything optional is nullable so model binding never
/// invents a "required" error; enum-like values stay strings so the validator can reject unknown ones with a message
/// naming the allowed values (they are parsed by <see cref="QueryValues"/> and <see cref="RequestSorting"/>).
/// </summary>
public sealed class RequestListQuery
{
    public const int MaxSearchLength = 100;
    public const int MaxPage = 1_000_000;
    public const int MaxPageSize = 100;
    public const int DefaultPageSize = 20;

    public string? Search { get; init; }

    public string[]? Status { get; init; }

    public string[]? Priority { get; init; }

    public string? OrganizationName { get; init; }

    public string? AssignedTo { get; init; }

    public DateOnly? CreatedFrom { get; init; }

    public DateOnly? CreatedTo { get; init; }

    public string? SortBy { get; init; }

    public string? SortDirection { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = DefaultPageSize;
}
