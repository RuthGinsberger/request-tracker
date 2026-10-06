using System.Buffers.Text;

namespace RequestTracker.Application;

/// <summary>Converts the 8-byte <c>rowversion</c> to and from the opaque base64 token used in DTOs and ETags.</summary>
public static class VersionToken
{
    private const int RowVersionLength = 8;

    public static string Encode(byte[] rowVersion) => Convert.ToBase64String(rowVersion);

    /// <summary>True only for canonical base64 of exactly 8 bytes.</summary>
    public static bool TryDecode(string? token, out byte[] rowVersion)
    {
        rowVersion = [];
        if (string.IsNullOrEmpty(token) || token.Length != Base64.GetMaxEncodedToUtf8Length(RowVersionLength))
        {
            return false;
        }

        Span<byte> buffer = stackalloc byte[RowVersionLength];
        if (!Convert.TryFromBase64String(token, buffer, out var written) || written != RowVersionLength)
        {
            return false;
        }

        rowVersion = buffer.ToArray();
        return true;
    }
}
