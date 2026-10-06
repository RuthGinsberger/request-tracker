using RequestTracker.Application;
using RequestTracker.Application.Abstractions;

namespace RequestTracker.Api.Http;

/// <summary>Reads <c>X-User-Name</c>; exposes it only when it passes <see cref="UserNameRules"/>.</summary>
public sealed class HeaderCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public const string HeaderName = "X-User-Name";

    public string? UserName =>
        httpContextAccessor.HttpContext is { } http
        && http.Request.Headers.TryGetValue(HeaderName, out var values)
        && values.Count == 1
        && UserNameRules.TryNormalize(values[0], out var name)
            ? name
            : null;
}
