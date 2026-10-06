namespace RequestTracker.Application.Abstractions;

/// <summary>
/// The caller identified by the required <c>X-User-Name</c> header (A8). <see cref="UserName"/> is the trimmed,
/// validated name, or null when the header is missing or invalid.
/// </summary>
public interface ICurrentUser
{
    string? UserName { get; }
}
