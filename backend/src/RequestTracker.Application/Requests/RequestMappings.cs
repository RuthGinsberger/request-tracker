using System.Linq.Expressions;
using RequestTracker.Domain;

namespace RequestTracker.Application.Requests;

/// <summary>The one place entities become DTOs. Expressions so EF can use them inside <c>Select</c>.</summary>
public static class RequestMappings
{
    public static readonly Expression<Func<Request, RequestDetailsDto>> ToDetails = r => new RequestDetailsDto(
        r.Id,
        r.Title,
        r.OrganizationName,
        r.Status,
        r.Priority,
        r.AssignedTo,
        r.CreatedAt,
        r.UpdatedAt,
        VersionToken.Encode(r.RowVersion));

    private static readonly Func<Request, RequestDetailsDto> ToDetailsCompiled = ToDetails.Compile();

    /// <summary>The same mapping for an entity already in memory (for example after a status change).</summary>
    public static RequestDetailsDto ToDetailsDto(Request request) => ToDetailsCompiled(request);

    public static readonly Expression<Func<Request, RequestListItemDto>> ToListItem = r => new RequestListItemDto(
        r.Id,
        r.Title,
        r.OrganizationName,
        r.Status,
        r.Priority,
        r.AssignedTo,
        r.CreatedAt,
        r.UpdatedAt,
        VersionToken.Encode(r.RowVersion));
}
