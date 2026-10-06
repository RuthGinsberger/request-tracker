using RequestTracker.Domain;

namespace RequestTracker.Application.Requests;

/// <summary>Public shape of one request; the API never returns the entity (R13).</summary>
/// <param name="Version">Base64 of the 8-byte rowversion; sent back in <c>If-Match</c> by updates.</param>
public sealed record RequestDetailsDto(
    int Id,
    string Title,
    string OrganizationName,
    RequestStatus Status,
    RequestPriority Priority,
    string? AssignedTo,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string Version);
