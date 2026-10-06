namespace RequestTracker.Domain;

public class Request
{
    public const int TitleMaxLength = 200;
    public const int OrganizationNameMaxLength = 200;
    public const int AssignedToMaxLength = 100;

    // For EF Core.
    private Request()
    {
        Title = string.Empty;
        OrganizationName = string.Empty;
        RowVersion = [];
    }

    public int Id { get; private set; }

    public string Title { get; private set; }

    public string OrganizationName { get; private set; }

    public RequestStatus Status { get; private set; }

    public RequestPriority Priority { get; private set; }

    /// <summary>Handler name or ID; null means unassigned (D-019).</summary>
    public string? AssignedTo { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    /// <summary>SQL Server <c>rowversion</c>, changed by the database on every UPDATE; the optimistic-concurrency token.</summary>
    public byte[] RowVersion { get; private set; }

    /// <summary>
    /// Creates a request. UpdatedAt equals CreatedAt on insert. Status defaults to New;
    /// the seeder passes other values to generate realistic data without history.
    /// </summary>
    public static Request Create(
        string title,
        string organizationName,
        RequestPriority priority,
        string? assignedTo,
        DateTime createdAtUtc,
        RequestStatus status = RequestStatus.New,
        DateTime? updatedAtUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(organizationName);
        EnsureUtc(createdAtUtc);
        if (updatedAtUtc is { } updated)
        {
            EnsureUtc(updated);
        }

        return new Request
        {
            Title = title,
            OrganizationName = organizationName,
            Priority = priority,
            AssignedTo = assignedTo,
            CreatedAt = createdAtUtc,
            UpdatedAt = updatedAtUtc ?? createdAtUtc,
            Status = status,
        };
    }

    /// <summary>
    /// Moves the request to <paramref name="newStatus"/> if <see cref="StatusTransitionPolicy"/> allows it, setting
    /// Status and UpdatedAt and producing the audit entry. A disallowed transition leaves the state untouched.
    /// Checking the client's expected version is the caller's job (Task 8).
    /// </summary>
    public StatusChangeResult ChangeStatus(RequestStatus newStatus, string changedBy, DateTime utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(changedBy);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            changedBy.Length, RequestStatusHistory.ChangedByMaxLength, nameof(changedBy));
        EnsureUtc(utcNow);

        if (!StatusTransitionPolicy.IsAllowed(Status, newStatus))
        {
            return new StatusChangeResult.NotAllowed(Status, newStatus);
        }

        var history = new RequestStatusHistory(Id, Status, newStatus, utcNow, changedBy);
        Status = newStatus;
        UpdatedAt = utcNow;
        return new StatusChangeResult.Changed(history);
    }

    private static void EnsureUtc(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Timestamps must be UTC.", nameof(value));
        }
    }
}
