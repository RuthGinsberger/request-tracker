namespace RequestTracker.Application.Requests;

/// <summary>The only fields the list can be sorted by. The query service maps each one to a column expression.</summary>
public enum RequestSortField
{
    CreatedAt,
    UpdatedAt,
    Priority,
    Status,
    Title,
}

public static class RequestSorting
{
    public const RequestSortField DefaultField = RequestSortField.CreatedAt;
    public const bool DefaultDescending = true;

    public static string AllowedFields => QueryValues.AllowedValues<RequestSortField>();

    /// <summary>Null/blank means the default; anything else must be a whitelisted name (case-insensitive).</summary>
    public static bool TryParseField(string? value, out RequestSortField field)
    {
        if (value is null)
        {
            field = DefaultField;
            return true;
        }

        return QueryValues.TryParseEnum(value, out field);
    }

    public static bool TryParseDirection(string? value, out bool descending)
    {
        descending = DefaultDescending;
        if (value is null)
        {
            return true;
        }

        if (string.Equals(value, "asc", StringComparison.OrdinalIgnoreCase))
        {
            descending = false;
            return true;
        }

        return string.Equals(value, "desc", StringComparison.OrdinalIgnoreCase);
    }
}
