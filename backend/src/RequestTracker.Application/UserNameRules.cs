namespace RequestTracker.Application;

/// <summary>Rules for the <c>X-User-Name</c> header (A8): trimmed, 1–100 characters.</summary>
public static class UserNameRules
{
    public const int MaxLength = 100;

    public static bool TryNormalize(string? raw, out string userName)
    {
        userName = raw?.Trim() ?? string.Empty;
        return userName.Length is >= 1 and <= MaxLength;
    }
}
