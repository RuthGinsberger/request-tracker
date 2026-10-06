using RequestTracker.Domain;

namespace RequestTracker.UnitTests.Domain;

public class StatusTransitionPolicyTests
{
    private static readonly HashSet<(RequestStatus From, RequestStatus To)> Allowed =
    [
        (RequestStatus.New, RequestStatus.InProgress),
        (RequestStatus.InProgress, RequestStatus.Waiting),
        (RequestStatus.Waiting, RequestStatus.InProgress),
        (RequestStatus.InProgress, RequestStatus.Completed),
        (RequestStatus.Waiting, RequestStatus.Completed),
    ];

    public static TheoryData<RequestStatus, RequestStatus, bool> AllPairs()
    {
        var data = new TheoryData<RequestStatus, RequestStatus, bool>();
        foreach (var from in Enum.GetValues<RequestStatus>())
        {
            foreach (var to in Enum.GetValues<RequestStatus>())
            {
                data.Add(from, to, Allowed.Contains((from, to)));
            }
        }

        return data;
    }

    [Fact]
    public void Matrix_covers_all_16_pairs() => Assert.Equal(16, AllPairs().Count);

    [Theory]
    [MemberData(nameof(AllPairs))]
    public void IsAllowed_matches_the_transition_table(RequestStatus from, RequestStatus to, bool expected) =>
        Assert.Equal(expected, StatusTransitionPolicy.IsAllowed(from, to));

    [Theory]
    [MemberData(nameof(AllPairs))]
    public void AllowedTargets_is_consistent_with_IsAllowed(RequestStatus from, RequestStatus to, bool expected) =>
        Assert.Equal(expected, StatusTransitionPolicy.AllowedTargets(from).Contains(to));

    [Fact]
    public void Completed_is_terminal() => Assert.Empty(StatusTransitionPolicy.AllowedTargets(RequestStatus.Completed));

    [Fact]
    public void Unknown_status_allows_nothing()
    {
        var unknown = (RequestStatus)99;
        Assert.False(StatusTransitionPolicy.IsAllowed(unknown, RequestStatus.New));
        Assert.Empty(StatusTransitionPolicy.AllowedTargets(unknown));
    }
}
