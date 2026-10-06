using Microsoft.Extensions.Primitives;
using RequestTracker.Application;

namespace RequestTracker.Api.Http;

/// <summary>Formats and parses the version token as a strong ETag; shared by every endpoint that uses a version.</summary>
public static class ETagHeader
{
    public static string Format(string version) => $"\"{version}\"";

    /// <summary>
    /// Accepts exactly one strong ETag whose opaque value is the base64 of an 8-byte rowversion. Rejects <c>*</c>,
    /// weak (<c>W/"..."</c>) and unquoted values, lists, and anything that does not decode.
    /// </summary>
    public static bool TryParseVersion(StringValues header, out byte[] version)
    {
        version = [];
        if (header.Count != 1 || header[0] is not { Length: > 2 } raw)
        {
            return false;
        }

        raw = raw.Trim();
        return raw.Length > 2
            && raw[0] == '"'
            && raw[^1] == '"'
            && VersionToken.TryDecode(raw[1..^1], out version);
    }
}
