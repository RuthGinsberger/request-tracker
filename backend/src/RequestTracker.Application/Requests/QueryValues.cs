namespace RequestTracker.Application.Requests;

/// <summary>Strict parsing of query-string values: names only (case-insensitive), never numbers.</summary>
public static class QueryValues
{
    public static bool TryParseEnum<TEnum>(string? value, out TEnum result) where TEnum : struct, Enum
    {
        foreach (var name in Enum.GetNames<TEnum>())
        {
            if (string.Equals(name, value, StringComparison.OrdinalIgnoreCase))
            {
                result = Enum.Parse<TEnum>(name);
                return true;
            }
        }

        result = default;
        return false;
    }

    public static string AllowedValues<TEnum>() where TEnum : struct, Enum => string.Join(", ", Enum.GetNames<TEnum>());

    /// <summary>Trims; blank becomes null, meaning "not provided".</summary>
    public static string? TrimToNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
