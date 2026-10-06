using RequestTracker.Domain;

namespace RequestTracker.UnitTests.Domain;

public class RequestTests
{
    private static readonly DateTime Created = new(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Now = new(2026, 2, 3, 9, 15, 0, DateTimeKind.Utc);

    private static Request NewRequest(RequestStatus status = RequestStatus.New) =>
        Request.Create("Title", "Org", RequestPriority.High, "dana", Created, status);

    [Fact]
    public void Create_sets_UpdatedAt_equal_to_CreatedAt_and_defaults_to_New()
    {
        var request = NewRequest();

        Assert.Equal(RequestStatus.New, request.Status);
        Assert.Equal(Created, request.CreatedAt);
        Assert.Equal(Created, request.UpdatedAt);
    }

    [Fact]
    public void ChangeStatus_allowed_updates_state_and_returns_history()
    {
        var request = NewRequest();

        var result = request.ChangeStatus(RequestStatus.InProgress, "dana", Now);

        var changed = Assert.IsType<StatusChangeResult.Changed>(result);
        Assert.Equal(RequestStatus.InProgress, request.Status);
        Assert.Equal(Now, request.UpdatedAt);
        Assert.Equal(Created, request.CreatedAt);
        Assert.Equal(request.Id, changed.History.RequestId);
        Assert.Equal(RequestStatus.New, changed.History.PreviousStatus);
        Assert.Equal(RequestStatus.InProgress, changed.History.NewStatus);
        Assert.Equal(Now, changed.History.ChangedAt);
        Assert.Equal("dana", changed.History.ChangedBy);
    }

    [Theory]
    [InlineData(RequestStatus.New, RequestStatus.New)]
    [InlineData(RequestStatus.New, RequestStatus.Completed)]
    [InlineData(RequestStatus.InProgress, RequestStatus.New)]
    [InlineData(RequestStatus.InProgress, RequestStatus.InProgress)]
    [InlineData(RequestStatus.Completed, RequestStatus.InProgress)]
    public void ChangeStatus_rejected_leaves_state_unchanged(RequestStatus from, RequestStatus to)
    {
        var request = NewRequest(from);

        var result = request.ChangeStatus(to, "dana", Now);

        var rejected = Assert.IsType<StatusChangeResult.NotAllowed>(result);
        Assert.Equal(from, rejected.From);
        Assert.Equal(to, rejected.To);
        Assert.Equal(from, request.Status);
        Assert.Equal(Created, request.UpdatedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ChangeStatus_rejects_blank_changedBy(string changedBy)
    {
        var request = NewRequest();

        Assert.Throws<ArgumentException>(() => request.ChangeStatus(RequestStatus.InProgress, changedBy, Now));
        Assert.Equal(RequestStatus.New, request.Status);
    }

    [Fact]
    public void ChangeStatus_rejects_too_long_changedBy() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            NewRequest().ChangeStatus(RequestStatus.InProgress, new string('x', 101), Now));

    [Fact]
    public void ChangeStatus_rejects_non_utc_time() =>
        Assert.Throws<ArgumentException>(() =>
            NewRequest().ChangeStatus(RequestStatus.InProgress, "dana", DateTime.SpecifyKind(Now, DateTimeKind.Local)));
}
