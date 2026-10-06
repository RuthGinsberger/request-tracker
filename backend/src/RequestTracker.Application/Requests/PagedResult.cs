namespace RequestTracker.Application.Requests;

/// <summary>One page of a larger result. <paramref name="TotalPages"/> is 0 when nothing matches.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount, int TotalPages)
{
    public static PagedResult<T> Create(IReadOnlyList<T> items, int page, int pageSize, int totalCount) =>
        new(items, page, pageSize, totalCount, (int)(((long)totalCount + pageSize - 1) / pageSize));
}
