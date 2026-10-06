using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RequestTracker.Application.Abstractions;
using RequestTracker.Domain;

namespace RequestTracker.Application.Requests;

public sealed class RequestQueryService(IAppDbContext db, ILogger<RequestQueryService> logger)
{
    public async Task<GetRequestResult> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var dto = await db.Requests
            .AsNoTracking()
            .Where(r => r.Id == id)
            .Select(RequestMappings.ToDetails)
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null ? new GetRequestResult.NotFound() : new GetRequestResult.Found(dto);
    }

    /// <summary>
    /// Filters, counts, sorts and pages in SQL Server; only the requested page is read. The query is assumed valid
    /// (see <see cref="RequestListQueryValidator"/>).
    /// </summary>
    public async Task<PagedResult<RequestListItemDto>> ListAsync(RequestListQuery query, CancellationToken cancellationToken)
    {
        var filtered = ApplyFilters(db.Requests.AsNoTracking(), query);

        var totalCount = await filtered.CountAsync(cancellationToken);

        RequestSorting.TryParseField(query.SortBy, out var sortField);
        RequestSorting.TryParseDirection(query.SortDirection, out var descending);

        // A page past the end needs no second round trip.
        var skip = (query.Page - 1) * query.PageSize;
        IReadOnlyList<RequestListItemDto> items = [];
        if (skip < totalCount)
        {
            items = await ApplySort(filtered, sortField, descending)
                .Skip(skip)
                .Take(query.PageSize)
                .Select(RequestMappings.ToListItem)
                .ToListAsync(cancellationToken);
        }

        logger.LogDebug(
            "Listed requests page {Page} (size {PageSize}) sorted by {SortField}: {ItemCount} of {TotalCount} matches",
            query.Page, query.PageSize, sortField, items.Count, totalCount);

        return PagedResult<RequestListItemDto>.Create(items, query.Page, query.PageSize, totalCount);
    }

    private static IQueryable<Request> ApplyFilters(IQueryable<Request> requests, RequestListQuery query)
    {
        var statuses = ParseAll<RequestStatus>(query.Status);
        if (statuses.Length > 0)
        {
            requests = requests.Where(r => statuses.Contains(r.Status));
        }

        var priorities = ParseAll<RequestPriority>(query.Priority);
        if (priorities.Length > 0)
        {
            requests = requests.Where(r => priorities.Contains(r.Priority));
        }

        if (!string.IsNullOrWhiteSpace(query.OrganizationName))
        {
            var organizationName = query.OrganizationName;
            requests = requests.Where(r => r.OrganizationName == organizationName);
        }

        if (!string.IsNullOrWhiteSpace(query.AssignedTo))
        {
            var assignedTo = query.AssignedTo;
            requests = requests.Where(r => r.AssignedTo == assignedTo);
        }

        if (query.CreatedFrom is { } from)
        {
            var start = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            requests = requests.Where(r => r.CreatedAt >= start);
        }

        // Inclusive through the whole day: CreatedAt < (to + 1 day). The year-9999 edge has no next day, so no bound.
        if (query.CreatedTo is { } to && to < DateOnly.MaxValue)
        {
            var endExclusive = to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            requests = requests.Where(r => r.CreatedAt < endExclusive);
        }

        if (QueryValues.TrimToNull(query.Search) is { } search)
        {
            var pattern = LikePattern.StartsWith(search);
            requests = requests.Where(r =>
                EF.Functions.Like(r.Title, pattern, LikePattern.EscapeCharacter)
                || EF.Functions.Like(r.OrganizationName, pattern, LikePattern.EscapeCharacter));
        }

        return requests;
    }

    /// <summary>Whitelisted sort fields only; Id in the same direction makes the order total, so paging is stable.</summary>
    private static IOrderedQueryable<Request> ApplySort(IQueryable<Request> requests, RequestSortField field, bool descending) =>
        field switch
        {
            RequestSortField.UpdatedAt => Order(requests, r => r.UpdatedAt, descending),
            RequestSortField.Priority => Order(requests, r => r.Priority, descending),
            RequestSortField.Status => Order(requests, r => r.Status, descending),
            RequestSortField.Title => Order(requests, r => r.Title, descending),
            _ => Order(requests, r => r.CreatedAt, descending),
        };

    private static IOrderedQueryable<Request> Order<TKey>(
        IQueryable<Request> requests, Expression<Func<Request, TKey>> key, bool descending) =>
        descending
            ? requests.OrderByDescending(key).ThenByDescending(r => r.Id)
            : requests.OrderBy(key).ThenBy(r => r.Id);

    private static TEnum[] ParseAll<TEnum>(string[]? values) where TEnum : struct, Enum =>
        values is null
            ? []
            : [.. values.Select(v => QueryValues.TryParseEnum<TEnum>(v, out var parsed) ? parsed : default).Distinct()];
}
