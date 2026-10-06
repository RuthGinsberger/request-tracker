using RequestTracker.Application.Requests;
using RequestTracker.Domain;

namespace RequestTracker.UnitTests.Application;

public sealed class StatusChangeClassifierTests
{
    private static readonly byte[] Current = [0, 0, 0, 0, 0, 0, 0x07, 0xD1];
    private static readonly byte[] Stale = [0, 0, 0, 0, 0, 0, 0x07, 0xD0];

    private static Request RequestWith(RequestStatus status, byte[] rowVersion)
    {
        var request = Request.Create(
            "t", "o", RequestPriority.Medium, null, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), status);
        typeof(Request).GetProperty(nameof(Request.RowVersion))!.SetValue(request, rowVersion);
        return request;
    }

    [Theory]
    [InlineData(RequestStatus.New, RequestStatus.InProgress)]
    [InlineData(RequestStatus.InProgress, RequestStatus.Waiting)]
    [InlineData(RequestStatus.InProgress, RequestStatus.Completed)]
    [InlineData(RequestStatus.Waiting, RequestStatus.InProgress)]
    [InlineData(RequestStatus.Waiting, RequestStatus.Completed)]
    public void Current_version_and_allowed_transition_is_allowed(RequestStatus from, RequestStatus to) =>
        Assert.Equal(
            StatusChangeCheck.Allowed,
            StatusChangeClassifier.Classify(RequestWith(from, Current), (byte[])Current.Clone(), to));

    [Theory]
    [InlineData(RequestStatus.New, RequestStatus.New)]
    [InlineData(RequestStatus.New, RequestStatus.Completed)]
    [InlineData(RequestStatus.Completed, RequestStatus.InProgress)]
    [InlineData(RequestStatus.Completed, RequestStatus.Completed)]
    [InlineData(RequestStatus.Waiting, RequestStatus.Waiting)]
    public void Current_version_and_forbidden_transition_is_rejected(RequestStatus from, RequestStatus to) =>
        Assert.Equal(
            StatusChangeCheck.TransitionNotAllowed,
            StatusChangeClassifier.Classify(RequestWith(from, Current), Current, to));

    [Fact]
    public void Stale_version_is_a_mismatch_even_for_an_allowed_transition() =>
        Assert.Equal(
            StatusChangeCheck.VersionMismatch,
            StatusChangeClassifier.Classify(RequestWith(RequestStatus.New, Current), Stale, RequestStatus.InProgress));

    [Fact]
    public void Stale_version_wins_over_a_forbidden_transition() =>
        Assert.Equal(
            StatusChangeCheck.VersionMismatch,
            StatusChangeClassifier.Classify(RequestWith(RequestStatus.Completed, Current), Stale, RequestStatus.New));

    [Fact]
    public void Versions_are_compared_by_value_not_by_reference() =>
        Assert.Equal(
            StatusChangeCheck.Allowed,
            StatusChangeClassifier.Classify(
                RequestWith(RequestStatus.New, [1, 2, 3, 4, 5, 6, 7, 8]),
                [1, 2, 3, 4, 5, 6, 7, 8],
                RequestStatus.InProgress));

    [Fact]
    public void Version_of_a_different_length_is_a_mismatch() =>
        Assert.Equal(
            StatusChangeCheck.VersionMismatch,
            StatusChangeClassifier.Classify(RequestWith(RequestStatus.New, Current), [0, 0, 0, 0, 0, 0, 0x07], RequestStatus.InProgress));
}
