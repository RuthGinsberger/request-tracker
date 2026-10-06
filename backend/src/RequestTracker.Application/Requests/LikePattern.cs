namespace RequestTracker.Application.Requests;

/// <summary>Builds <c>LIKE</c> patterns from user text so wildcard characters are matched literally.</summary>
public static class LikePattern
{
    public const string EscapeCharacter = "\\";

    /// <summary>Pattern for "starts with <paramref name="prefix"/>", to be used with <see cref="EscapeCharacter"/>.</summary>
    public static string StartsWith(string prefix)
    {
        var builder = new System.Text.StringBuilder(prefix.Length + 1);
        foreach (var c in prefix)
        {
            if (c is '\\' or '%' or '_' or '[')
            {
                builder.Append('\\');
            }

            builder.Append(c);
        }

        return builder.Append('%').ToString();
    }
}
