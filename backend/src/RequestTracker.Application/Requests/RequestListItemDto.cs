using RequestTracker.Domain;

namespace RequestTracker.Application.Requests;

/// <summary>One row of the list; same fields as the details DTO but a separate type so they can diverge.</summary>
/// <param name="Version">Base64 of the 8-byte rowversion; sent back in <c>If-Match</c> by updates.</param>
public sealed record RequestListItemDto(
    int Id,
    string Title,
    string OrganizationName,
    RequestStatus Status,
    RequestPriority Priority,
    string? AssignedTo,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string Version);
